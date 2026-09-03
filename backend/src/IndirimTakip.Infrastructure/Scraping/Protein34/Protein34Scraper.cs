using System.Globalization;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Protein34;

/// <summary>
/// protein34.com — yirmi dokuzuncu kaynak, DÖRDÜNCÜ BAYİ (protein7,
/// Provitamin ve Fit Çarşı'dan sonra). IdeaSoft altyapısı.
///
/// <b>NEDEN EKLENDİ (kullanıcı kararı, ölçüm sunulduktan sonra).</b>
/// 3 Eylül ölçümü iki sınırlama gösterdi: ürünlerin %72'si stok dışı ve
/// taşıdığı 14 markanın HEPSİ katalogda zaten var (yeni üretici getirmiyor).
/// Yani katkısı "aynı ürün için bir fiyat noktası daha". Kullanıcı bunu
/// bilerek istedi: fiyat karşılaştırma sitesinin işi zaten bu — aynı ürünü
/// biri daha ucuza satıyorsa kullanıcı görsün.
///
/// <b>MARKA SAYFADAN OKUNUYOR, isimden tahmin EDİLMİYOR.</b> Sayfadaki
/// <c>brandName</c> alanı üreticiyi veriyor. Adlar <c>BrandNameNormalizer</c>
/// üzerinden kanonik yazıma çevriliyor ("Bigjoy Sports" -> "BigJoy",
/// "Nuclear" -> "Nuclear Nutrition", "KEVİN LEVRONE" -> "Kevin Levrone");
/// çevrilmezse kopya <c>Brand</c> kaydı oluşur.
///
/// <b>MAĞAZA ESKİ FİYATI BİLİNÇLİ OLARAK ALINMIYOR.</b> Sayfadaki
/// <c>product-price-old</c> kutusu ölçülen örnekte güncel fiyatın AYNISINI
/// yazıyordu; okunsaydı olmayan bir "mağaza indirimi" üretirdi. Sitenin
/// iddiası gerçek fiyat geçmişine dayandığı için uydurma indirim
/// gösterilmesi tam da kaçınılan şey.
///
/// Maliyet: 228 adres, ürün başına bir istek (~1 dk). protein7 (~900) ve
/// Provitamin (~430) gibi <c>DailyOnly</c> olmayı gerektirmiyor.
/// </summary>
public sealed partial class Protein34Scraper(HttpClient httpClient, ILogger<Protein34Scraper> logger)
    : IBrandScraper
{
    // Ürünün kendi markası okunamazsa kullanılacak ad (pratikte olmuyor;
    // ölçümde 223 ürünün 223'ünde de marka okundu).
    public string BrandName => "Protein34";
    public string BaseUrl => "https://www.protein34.com";

    private const string SellerName = "protein34.com";

    private const string SitemapUrl =
        "https://www.protein34.com/xml/sitemap_product_1.xml?sr=6a5f0923b0dab";

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(250);

    // GERÇEK hataların oranı bunu aşarsa tarama güvenilmez sayılıyor.
    // Ölçümde 228 adresin 5'i (%2) veri vermedi — silinmiş ürünler; protein7'de
    // de aynı durum var (sitemap silinmiş ürünleri listelemeye devam ediyor).
    private const double MaxFailureRatio = 0.2;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var xml = await httpClient.GetStringAsync(SitemapUrl, cancellationToken);
        var urls = LocRegex().Matches(xml)
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim())
            .Where(u => u.Contains("/urun/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (urls.Count == 0)
            throw new InvalidOperationException("protein34: sitemap'te hiç ürün adresi bulunamadı.");

        var result = new List<ScrapedProduct>();
        var failures = 0;
        var filtered = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync(url, cancellationToken);
                var product = ParseProduct(html, url);

                if (product is null)
                    filtered++;
                else
                    result.Add(product);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        if (failures > urls.Count * MaxFailureRatio)
        {
            throw new InvalidOperationException(
                $"protein34: {urls.Count} adresin {failures} tanesinde hata oluştu, tarama güvenilir değil.");
        }

        logger.LogInformation(
            "protein34: {Total} adres tarandı, {Found} ürün alındı, {Filtered} süzüldü, {Failures} hata.",
            urls.Count, result.Count, filtered, failures);

        return result;
    }

    /// <summary>
    /// Ürün sayfasından ürün. Adı/fiyatı okunamayan sayfada null döner.
    /// </summary>
    internal static ScrapedProduct? ParseProduct(string html, string url)
    {
        var nameMatch = NameRegex().Match(html);
        if (!nameMatch.Success)
            return null;

        var name = System.Net.WebUtility.HtmlDecode(nameMatch.Groups[1].Value).Trim();
        if (name.Length == 0)
            return null;

        if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return null;

        // FİYATTA TIRNAKLAR KARIŞIK: itemprop TEK, content ÇİFT tırnaklı
        // (itemprop='price' content="879.00"). İlk denemede tutarlı tırnak
        // varsayıldığı için hiçbir üründe fiyat okunamamış, kaynak "fiyat
        // vermiyor" sanılmıştı.
        var priceMatch = PriceRegex().Match(html);
        if (!priceMatch.Success)
            return null;

        // Değer NOKTA ondalıklı ("879.00") — Türkçe kültürle ayrıştırılsaydı
        // 87900 çıkardı.
        if (!decimal.TryParse(priceMatch.Groups[1].Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var price) || price <= 0)
        {
            return null;
        }

        var brandMatch = BrandRegex().Match(html);
        var brand = brandMatch.Success
            ? BrandNameNormalizer.Normalize(System.Net.WebUtility.HtmlDecode(brandMatch.Groups[1].Value).Trim())
            : null;

        bool? inStock = null;
        var stockMatch = StockRegex().Match(html);
        if (stockMatch.Success)
        {
            var value = stockMatch.Groups[1].Value;
            if (value.Equals("InStock", StringComparison.OrdinalIgnoreCase))
                inStock = true;
            else if (value.Equals("OutOfStock", StringComparison.OrdinalIgnoreCase))
                inStock = false;
        }

        var imageMatch = ImageRegex().Match(html);
        string? image = null;
        if (imageMatch.Success)
        {
            var raw = imageMatch.Groups[1].Value.Trim();
            // Adres protokolsüz geliyor ("//www.protein34.com/...").
            image = raw.StartsWith("//", StringComparison.Ordinal) ? "https:" + raw : raw;
        }

        return new ScrapedProduct(
            Name: name,
            Url: url,
            ImageUrl: image,
            // Kaynağın kendi kategorileri bizim slug'larımıza oturmuyor;
            // kategori ürün adından çıkarılıyor.
            Category: null,
            Price: price,
            BrandName: string.IsNullOrWhiteSpace(brand) ? null : brand,
            InStock: inStock,
            Seller: SellerName);
    }

    [GeneratedRegex(@"<loc>([^<]+)</loc>", RegexOptions.IgnoreCase)]
    private static partial Regex LocRegex();

    [GeneratedRegex(@"<h1[^>]*>([^<]{3,160})</h1>", RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"itemprop='price'[^>]*content=""([\d.]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"brandName:\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex BrandRegex();

    [GeneratedRegex(@"availability'\s*href='https://schema\.org/(\w+)'", RegexOptions.IgnoreCase)]
    private static partial Regex StockRegex();

    [GeneratedRegex(@"itemprop=""image""[^>]*src=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ImageRegex();
}

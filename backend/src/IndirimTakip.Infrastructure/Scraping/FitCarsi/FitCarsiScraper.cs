using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.FitCarsi;

/// <summary>
/// fitcarsi.com.tr — yirmi ikinci kaynak, ÜÇÜNCÜ BAYİ (protein7 ve
/// Provitamin'den sonra). Özel ASP.NET WebForms mağazası.
///
/// <b>MARKA SAYFALARINDAN OKUNUYOR, kategori sayfalarından değil.</b> İki
/// yol da ölçüldü (2 Eylül): 39 kategori sayfası 295 benzersiz ürün veriyor,
/// 25 marka sayfası 285 — yani kategoriler biraz daha kapsamlı. Yine de
/// marka sayfaları seçildi, çünkü markayı TAHMİN ETMEK ZORUNDA KALMIYORUZ:
/// hangi sayfadan geldiyse markası odur. Kategori yolunda marka ürün adından
/// çıkarılmak zorunda kalırdı ve bu veride ürün adları buna elverişsiz
/// (bkz. CLAUDE.md, bayi/marka ayrımı notu). Kaçan ~10 ürünün sitede de
/// markası yok, yani orada da tahmin gerekirdi.
///
/// Maliyet: 25 istek. protein7 (~900) ve Provitamin (~430) gibi ürün başına
/// istek atmadığı için <c>DailyOnly</c> DEĞİL, 6 saatlik genel tura giriyor.
///
/// <b>ÖLÇÜM TUZAĞI (buraya iki kez düşüldü):</b> ilk incelemede "ürünler JS
/// ile geliyor" sanıldı, çünkü (a) fiyat "790,00 TL" diye bitişik arandı ama
/// site TL'yi ayrı bir &lt;span&gt;'de yazıyor, (b) ürün bağlantısı "/Urun"
/// deseniyle arandı ama adresler kökte "/{slug}-{id}.aspx" biçiminde.
/// Sayfa baştan beri sunucu tarafında render ediliyormuş.
/// </summary>
public partial class FitCarsiScraper(HttpClient httpClient, ILogger<FitCarsiScraper> logger) : IBrandScraper
{
    // Ürünün kendi markası okunamazsa kullanılacak ad (pratikte olmuyor:
    // marka sayfasından geldiği için her ürünün markası biliniyor).
    public string BrandName => "Fit Çarşı";
    public string BaseUrl => "https://www.fitcarsi.com.tr";

    private const string SellerName = "fitcarsi.com.tr";

    // Nezaket beklemesi — 25 istek için maliyeti yok.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    // GERÇEK hataların oranı bunu aşarsa tarama güvenilmez sayılıyor.
    private const double MaxFailureRatio = 0.2;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var brands = await FetchBrandsAsync(cancellationToken);
        if (brands.Count == 0)
            throw new InvalidOperationException("Fit Çarşı: ana sayfada hiç marka bağlantısı bulunamadı.");

        var result = new List<ScrapedProduct>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = 0;
        var filtered = 0;

        foreach (var (brandUrl, brandLabel) in brands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync(BaseUrl + brandUrl, cancellationToken);
                var brand = BrandNameNormalizer.Normalize(WebDecode(brandLabel));

                foreach (Match card in ProductCardRegex().Matches(html))
                {
                    var slug = card.Groups["slug"].Value;
                    var name = WebDecode(card.Groups["name"].Value).Trim();
                    if (slug.Length == 0 || name.Length == 0)
                        continue;

                    // Aynı ürün birden çok marka sayfasında görünmemeli ama
                    // garanti değil; adres bazında tekilleştiriliyor.
                    var url = $"{BaseUrl}{slug}";
                    if (!seen.Add(url))
                        continue;

                    if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
                    {
                        filtered++;
                        continue;
                    }

                    if (!TryParsePrice(card.Groups["price"].Value, out var price))
                        continue;

                    var image = card.Groups["image"].Value;

                    result.Add(new ScrapedProduct(
                        Name: name,
                        Url: url,
                        ImageUrl: image.Length > 0 ? BaseUrl + image : null,
                        // Sitenin kendi kategorileri bizim slug'larımıza
                        // oturmuyor; kategori isimden çıkarılıyor.
                        Category: null,
                        Price: price,
                        // Marka SAYFADAN geliyor, isimden tahmin edilmiyor.
                        BrandName: brand,
                        // Stok durumu ürün KARTINDA yok, yalnızca ürün
                        // sayfasında. 285 ürün için ayrı istek atmaya değmez;
                        // "bilmiyoruz" bırakılıyor — false ile karıştırılmamalı.
                        InStock: null,
                        Seller: SellerName));
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        if (failures > brands.Count * MaxFailureRatio)
        {
            throw new InvalidOperationException(
                $"Fit Çarşı: {brands.Count} marka sayfasının {failures} tanesinde hata oluştu, " +
                "tarama güvenilir değil.");
        }

        logger.LogInformation(
            "Fit Çarşı: {Brands} marka sayfası tarandı, {Found} ürün alındı, {Filtered} takviye dışı süzüldü, {Failures} hata.",
            brands.Count, result.Count, filtered, failures);

        return result;
    }

    /// <summary>Ana sayfadaki marka bağlantıları: adres + görünen ad.</summary>
    private async Task<List<(string Url, string Label)>> FetchBrandsAsync(CancellationToken cancellationToken)
    {
        var html = await httpClient.GetStringAsync(BaseUrl + "/", cancellationToken);
        return BrandLinkRegex().Matches(html)
            .Select(m => (Url: m.Groups["url"].Value, Label: m.Groups["label"].Value.Trim()))
            .Where(x => x.Url.Length > 0 && x.Label.Length > 0)
            .DistinctBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// "890,00" -> 890.00. Kartta tutar ile "TL" AYRI elemanlarda olduğu için
    /// TurkishPriceParser'a birim beklemeden ham tutar veriliyor.
    /// </summary>
    private static bool TryParsePrice(string raw, out decimal price)
    {
        price = 0;
        var text = raw.Trim();
        if (text.Length == 0)
            return false;

        try
        {
            price = TurkishPriceParser.Parse(text);
        }
        catch (FormatException)
        {
            return false;
        }

        return price > 0;
    }

    /// <summary>ASP.NET çıktısı HTML varlıklarıyla dolu ("&amp;#199;", "&amp;amp;").</summary>
    private static string WebDecode(string value) =>
        System.Net.WebUtility.HtmlDecode(value);

    [GeneratedRegex(
        @"href=""(?<url>/Markalar/[a-z0-9-]+-\d+\.aspx)""[^>]*>\s*(?<label>[^<]{1,60}?)\s*<",
        RegexOptions.IgnoreCase)]
    private static partial Regex BrandLinkRegex();

    // Kart yapısı sabit: görsel bağlantısı, ad bağlantısı, sonra fiyat kutusu.
    // Fiyattaki "TL" ayrı bir <span>'de, o yüzden kalıba dahil edilmiyor.
    [GeneratedRegex(
        @"class=""product-img""\s+href=""(?<slug>/[a-z0-9-]+-\d+\.aspx)"">\s*<img[^>]*src=""(?<image>/Uploads/[^""]+)""" +
        @".*?class=""product-name""[^>]*>(?<name>[^<]+)<" +
        @".*?class=""product-price""[^>]*>\s*(?<price>[\d.,]+)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ProductCardRegex();
}

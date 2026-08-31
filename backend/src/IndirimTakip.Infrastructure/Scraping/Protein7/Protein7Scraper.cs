using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Protein7;

/// <summary>
/// protein7.com — T-Soft altyapılı bir BAYİ (çok markalı). Bizim diğer
/// kaynaklarımızdan iki temel farkı var:
///
/// 1. <b>Çok markalı.</b> Her ürün kendi üreticisini taşıyor (BigJoy,
///    Optimum, Nutrend...). Ürün o markanın altında görünüyor,
///    <see cref="ScrapedProduct.Seller"/> ise satın alınan yeri söylüyor.
///    Barkod (GTIN) yayınlanmadığı için aynı ürünün başka satıcıdaki kaydıyla
///    EŞLEŞTİRME YAPILMIYOR; iki ayrı kayıt olarak duruyorlar.
///
/// 2. <b>Ürün başına bir istek.</b> Kategori sayfalarındaki liste tarayıcıda
///    çiziliyor, sunucudan gelen HTML'de ürün yok — bu yüzden liste yerine
///    sitemap'teki ~900 ürün adresi tek tek geziliyor. Maliyeti yüzünden bu
///    scraper <see cref="DailyOnly"/> işaretli: 6 saatlik genel tura değil,
///    günde bir kez çalışan tura giriyor.
///
/// Ayrıştırma, sayfadaki OpenGraph etiketleri ve gömülü (kaçışlanmış) Product
/// şemasından yapılıyor. Sayfada gerçek bir &lt;script type="application/ld+json"&gt;
/// Product bloğu YOK — şema JS içinde string olarak duruyor, bu yüzden JSON
/// ayrıştırıcı değil hedefli regex kullanılıyor.
/// </summary>
public partial class Protein7Scraper(HttpClient httpClient) : IBrandScraper
{
    // Ürünün kendi markası okunamazsa kullanılacak ad (mağazanın kendi
    // ürünleri de var: "Protein 7 Pill Box" gibi).
    public string BrandName => "Protein7";
    public string BaseUrl => "https://protein7.com";

    // Ürün başına istek attığı için genel tura dahil değil.
    public bool DailyOnly => true;

    private const string SellerName = "protein7.com";
    private const string ProductSitemapUrl = "https://protein7.com/xml/sitemap/product.xml";

    // Nezaket beklemesi: ~900 istek atıyoruz, karşı sunucuyu yormamak ve
    // engellenmemek için. Diğer scraper'lardaki 400-500 ms ile aynı düzen.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchProductUrlsAsync(cancellationToken);
        var result = new List<ScrapedProduct>();

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var product = await FetchProductAsync(url, cancellationToken);
                if (product is not null)
                    result.Add(product);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Tek bir ürün sayfasının hatası (404, zaman aşımı, bozuk HTML)
                // ~900 ürünlük taramanın tamamını düşürmemeli.
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        return result;
    }

    private async Task<List<string>> FetchProductUrlsAsync(CancellationToken cancellationToken)
    {
        var xml = await httpClient.GetStringAsync(ProductSitemapUrl, cancellationToken);
        return LocRegex().Matches(xml)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(u => u.Length > 0)
            .Distinct()
            .ToList();
    }

    private async Task<ScrapedProduct?> FetchProductAsync(string url, CancellationToken cancellationToken)
    {
        var html = await httpClient.GetStringAsync(url, cancellationToken);

        var name = OgTitleRegex().Match(html).Groups[1].Value.Trim();
        if (name.Length == 0)
            return null;

        if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return null;

        var priceText = PriceRegex().Match(html).Groups[1].Value;
        if (!decimal.TryParse(priceText, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price) || price <= 0)
            return null;

        var brand = BrandRegex().Match(html).Groups[1].Value.Trim();
        var image = OgImageRegex().Match(html).Groups[1].Value.Trim();

        // "in stock" / "out of stock". Etiket hiç yoksa null bırakılıyor —
        // "bilmiyoruz" ile "stokta yok" karıştırılmamalı.
        var availability = AvailabilityRegex().Match(html).Groups[1].Value.Trim();
        bool? inStock = availability.Length == 0
            ? null
            : availability.Contains("in stock", StringComparison.OrdinalIgnoreCase)
                && !availability.Contains("out of stock", StringComparison.OrdinalIgnoreCase);

        return new ScrapedProduct(
            Name: name,
            Url: url,
            ImageUrl: image.Length > 0 ? image : null,
            // Sitenin kendi kategorisi ("Amino Asit >BCAA") bizim slug'larımıza
            // birebir oturmuyor; isimden çıkarım diğer markalarla tutarlı
            // sonuç veriyor (bkz. ProductAttributeParser.InferCategory).
            Category: null,
            Price: price,
            // Üretici markası ürün başına geliyor; okunamazsa mağazanın kendi adı.
            BrandName: brand.Length > 0 ? brand : null,
            InStock: inStock,
            Seller: SellerName);
    }

    [GeneratedRegex(@"<loc>([^<]+)</loc>", RegexOptions.IgnoreCase)]
    private static partial Regex LocRegex();

    [GeneratedRegex(@"<meta\s+property=""og:title""\s+content=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitleRegex();

    [GeneratedRegex(@"<meta\s+property=""og:image""\s+content=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgImageRegex();

    [GeneratedRegex(@"<meta\s+property=""product:availability""\s+content=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AvailabilityRegex();

    // Gömülü Product şeması JS içinde kaçışlanmış bir string olarak duruyor,
    // bu yüzden JSON olarak ayrıştırılamıyor.
    [GeneratedRegex(@"""brand"":\{[^}]*?""name"":""([^""]*)""")]
    private static partial Regex BrandRegex();

    [GeneratedRegex(@"""price"":""([0-9]+(?:\.[0-9]+)?)""")]
    private static partial Regex PriceRegex();
}

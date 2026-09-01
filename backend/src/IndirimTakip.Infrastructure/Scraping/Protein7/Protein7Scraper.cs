using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

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
public partial class Protein7Scraper(HttpClient httpClient, ILogger<Protein7Scraper> logger) : IBrandScraper
{
    // Ürünün kendi markası okunamazsa kullanılacak ad (mağazanın kendi
    // ürünleri de var: "Protein 7 Pill Box" gibi).
    public string BrandName => "Protein7";
    public string BaseUrl => "https://protein7.com";

    // Ürün başına istek attığı için genel tura dahil değil.
    public bool DailyOnly => true;

    private const string SellerName = "protein7.com";
    private const string ProductSitemapUrl = "https://protein7.com/xml/sitemap/product.xml";

    // Nezaket beklemesi. Hız sınırı YOK — VM'den 300 ardışık istek 0,3 sn
    // aralıkla sorunsuz geçti. Bu değer tamamen nezaket: günde bir çalışan
    // bir tarama için 15 dakika sorun değil, karşı sunucuyu yormamak yeğdir.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(1);

    // GERÇEK hataların oranı bunu aşarsa tarama güvenilmez sayılıyor.
    // 404'ler buna DAHİL DEĞİL: sitemap silinmiş ürünlerin adreslerini de
    // listeliyor (ölçüldü: 914 adresin ~420'si 404) ve bu beklenen bir durum,
    // hata değil. İlk sürümde 404'ler de sayıldığı için tarama "hız sınırına
    // takıldı" sanılıp iki kez boşuna yavaşlatıldı.
    private const double MaxFailureRatio = 0.2;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchProductUrlsAsync(cancellationToken);
        var result = new List<ScrapedProduct>();
        var failures = 0;
        var missing = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var product = await FetchProductAsync(url, cancellationToken);
                if (product is not null)
                    result.Add(product);
            }
            catch (ProductGoneException)
            {
                // Sitemap'te olan ama artık var olmayan ürün. Beklenen durum,
                // hata değil — güvenilirlik hesabına girmiyor.
                missing++;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Zaman aşımı, 5xx, bozuk HTML: tek sayfa yüzünden ~900
                // ürünlük tarama düşmemeli ama SAYILIYOR — sessiz veri kaybı
                // gürültülü hatadan tehlikelidir.
                failures++;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        if (urls.Count > 0 && failures > urls.Count * MaxFailureRatio)
        {
            // Yarısı eksik bir katalogla sessizce devam etmek yerine hata ver:
            // yutma servisi mevcut ürünleri silmiyor, dolayısıyla veri
            // kaybolmuyor — ama sorun log'a düşüyor ve fark ediliyor.
            throw new InvalidOperationException(
                $"protein7: {urls.Count} adresin {failures} tanesinde beklenmeyen hata oluştu, " +
                "tarama güvenilir değil.");
        }

        logger.LogInformation(
            "protein7: {Total} adres tarandı, {Found} ürün alındı, {Missing} adres artık yok (404), {Failures} hata.",
            urls.Count, result.Count, missing, failures);

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

    /// <summary>Sitemap'te duran ama artık yayında olmayan ürün.</summary>
    private sealed class ProductGoneException : Exception;

    private async Task<ScrapedProduct?> FetchProductAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new ProductGoneException();

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

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
            // Ortak normalizasyon ŞART: protein7 "Proteinocean" yazıyor,
            // markanın kendi sitesi "ProteinOcean". Normalize edilmeyince marka
            // ikiye bölünüyor, ikisi de aynı adrese çözülüyor ve sitemap'e
            // tekrar eden adresler giriyordu (bkz. BrandNameNormalizer).
            BrandName: brand.Length > 0 ? BrandNameNormalizer.Normalize(brand) : null,
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

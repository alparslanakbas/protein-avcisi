using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// "sitemap'ten adresleri al, her ürün sayfasındaki schema.org bloğunu oku"
/// deseninin ortak gövdesi. Gigi's ve MLA Protein bunu kullanıyor.
///
/// Ayrı bir taban sınıf yazıldı çünkü iki scraper arasındaki tek fark ad,
/// adres ve markanın sabit mi yoksa sayfadan mı geleceği; gövdeyi kopyalamak
/// aynı hatayı iki yerde düzeltmek anlamına gelirdi.
/// </summary>
public abstract class SitemapSchemaOrgScraper(HttpClient httpClient, ILogger logger) : IBrandScraper
{
    public abstract string BrandName { get; }
    public abstract string BaseUrl { get; }

    /// <summary>Ürün adreslerini taşıyan sitemap.</summary>
    /// <summary>
    /// Türeyen sınıfların (ör. besin değeri için ürün sayfasını ayrıca okuyan
    /// çekiciler) kullanabilmesi için. Birincil oluşturucu parametresini
    /// türeyen sınıfta ayrıca yakalamak derleyici uyarısı üretiyor (CS9107)
    /// ve aynı örneğe ikinci bir referans tutuyordu.
    /// </summary>
    protected HttpClient Http { get; } = httpClient;

    protected abstract string SitemapUrl { get; }

    /// <summary>
    /// Markayı ürün sayfasından mı okuyalım? Çok markalı mağazalarda true.
    /// </summary>
    protected virtual bool ReadBrandFromPage => false;

    /// <summary>Nezaket beklemesi — katalog küçük, maliyeti düşük.</summary>
    protected virtual TimeSpan DelayBetweenRequests => TimeSpan.FromMilliseconds(250);

    /// <summary>Bu oranın üstünde hata varsa tarama güvenilmez sayılıyor.</summary>
    protected virtual double MaxFailureRatio => 0.2;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var xml = await httpClient.GetStringAsync(SitemapUrl, cancellationToken);
        var urls = IkasSchemaOrgCatalog.ParseSitemap(xml);
        if (urls.Count == 0)
            throw new InvalidOperationException($"{BrandName}: sitemap'te hiç ürün adresi bulunamadı.");

        var result = new List<ScrapedProduct>();
        var failures = 0;
        var filtered = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync(url, cancellationToken);
                var product = IkasSchemaOrgCatalog.ParseProduct(
                    html, url, ReadBrandFromPage ? null : BrandName);

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
                $"{BrandName}: {urls.Count} adresin {failures} tanesinde hata oluştu, tarama güvenilir değil.");
        }

        logger.LogInformation(
            "{Brand}: {Total} adres tarandı, {Found} ürün alındı, {Filtered} süzüldü, {Failures} hata.",
            BrandName, urls.Count, result.Count, filtered, failures);

        return result;
    }
}

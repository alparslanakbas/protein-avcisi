namespace IndirimTakip.Core.Scraping;

public interface IBrandScraper
{
    string BrandName { get; }
    string BaseUrl { get; }

    Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default);
}

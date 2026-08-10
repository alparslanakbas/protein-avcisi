namespace IndirimTakip.Core.Scraping;

public interface IBrandScraper
{
    string BrandName { get; }

    Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default);
}

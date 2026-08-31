using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

public class ScrapingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ScrapingBackgroundService> logger) : BackgroundService
{
    // İstekler arası nezaket bekleme: markanın sitesini yormamak, IP engellenme riskini azaltmak için.
    private static readonly TimeSpan DelayBetweenBrands = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Scraping:Enabled", true))
        {
            logger.LogInformation("Zamanlanmış tarama devre dışı (Scraping:Enabled=false).");
            return;
        }

        var intervalHours = configuration.GetValue("Scraping:IntervalHours", 6);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        do
        {
            await RunScrapeCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunScrapeCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        // DailyOnly işaretliler bu turun DIŞINDA: onları
        // DailyScrapingBackgroundService günde bir kez çalıştırıyor.
        var scrapers = scope.ServiceProvider.GetServices<IBrandScraper>()
            .Where(s => !s.DailyOnly)
            .ToList();
        var ingestion = scope.ServiceProvider.GetRequiredService<ScrapeIngestionService>();

        logger.LogInformation("Tarama döngüsü başladı ({Count} marka).", scrapers.Count);

        foreach (var scraper in scrapers)
        {
            try
            {
                var count = await ingestion.IngestAsync(scraper, cancellationToken);
                logger.LogInformation("{Brand}: {Count} ürün tarandı.", scraper.BrandName, count);
            }
            catch (Exception ex)
            {
                // Bir markanın taraması başarısız olsa bile diğerleri devam etmeli.
                logger.LogError(ex, "{Brand} taranırken hata oluştu.", scraper.BrandName);
            }

            await Task.Delay(DelayBetweenBrands, cancellationToken);
        }

        logger.LogInformation("Tarama döngüsü bitti.");
    }
}

using IndirimTakip.Core.Caching;
using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// <see cref="IBrandScraper.DailyOnly"/> işaretli kaynakları günde bir kez,
/// gece yarısı (Türkiye saati) tarar.
///
/// Neden ayrı bir servis: genel tarama turu 6 saatte bir çalışıyor ve
/// başlangıcı uygulamanın açıldığı ana bağlı, yani belirli bir saate
/// denk getirilemiyor. Günlük kaynaklar için sabit bir saat gerekiyordu —
/// gün değişiminde taramak, bir günün fiyatını o güne ait tek bir ölçümle
/// temsil etmeyi kolaylaştırıyor.
///
/// Bu ayrımın sebebi maliyet: bu kaynaklarda ürün listesi tarayıcıda
/// çizildiği için ürün başına ayrı istek atmak gerekiyor. 900+ ürünü 6
/// saatte bir çekmek karşı sunucuya günde binlerce istek demek olurdu ve
/// engellenme riskini ciddi biçimde artırırdı.
///
/// Zamanlama veritabanında (bkz. PersistedSchedule.RunDailyAsync): deploy
/// taramayı yarıda keserse açılışta hemen yeniden çalışıyor; eskiden ertesi
/// gece yarısını bekliyordu ve o günün verisi kayboluyordu.
/// </summary>
public class DailyScrapingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DailyScrapingBackgroundService> logger) : BackgroundService
{
    // Türkiye saati UTC+3, dolayısıyla 00:00 TSİ = 21:00 UTC. Sabit ofset
    // kullanılıyor çünkü Türkiye 2016'dan beri yaz saati uygulamıyor —
    // kalıcı olarak UTC+3.
    private const int RunAtUtcHour = 21;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        configuration.GetValue("Scraping:Enabled", true)
            ? PersistedSchedule.RunDailyAsync(
                scopeFactory, BackgroundJobNames.GunlukTarama, RunAtUtcHour, logger, RunAsync, stoppingToken)
            : Task.CompletedTask;

    private async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var scrapers = services.GetServices<IBrandScraper>()
            .Where(s => s.DailyOnly)
            .ToList();

        if (scrapers.Count == 0)
            return;

        var ingestion = services.GetRequiredService<ScrapeIngestionService>();
        logger.LogInformation("Günlük tarama başladı ({Count} kaynak).", scrapers.Count);

        foreach (var scraper in scrapers)
        {
            try
            {
                var count = await ingestion.IngestAsync(scraper, cancellationToken);
                logger.LogInformation("{Brand}: {Count} ürün tarandı (günlük).", scraper.BrandName, count);
            }
            catch (Exception ex)
            {
                // Bir kaynağın taraması başarısız olsa bile diğerleri devam etmeli.
                logger.LogError(ex, "{Brand} taranırken hata oluştu (günlük).", scraper.BrandName);
            }
        }

        // Fiyat özeti ÖNCE: önbellek ısıtması bu alanları okuyor, ters
        // sırada ısıtma eski özeti önbelleğe alırdı.
        await services.GetRequiredService<PriceSummaryRefresher>()
            .RefreshAsync(cancellationToken);

        await services.GetRequiredService<IPublicCacheRefresher>()
            .RefreshAsync(cancellationToken);

        logger.LogInformation("Günlük tarama bitti.");
    }
}

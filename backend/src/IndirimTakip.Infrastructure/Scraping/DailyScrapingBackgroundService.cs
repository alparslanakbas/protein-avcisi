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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Scraping:Enabled", true))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var bekleme = NextRunDelay(DateTimeOffset.UtcNow);
            logger.LogInformation(
                "Günlük tarama {Saat} sonra çalışacak (00:00 Türkiye saati).",
                bekleme);

            try
            {
                await Task.Delay(bekleme, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Bir sonraki 21:00 UTC'ye kalan süre. Saat tam denk gelirse bir sonraki
    /// güne kaydırılıyor: aksi halde tarama biter bitmez sıfır beklemeyle
    /// tekrar tetiklenebilirdi.
    /// </summary>
    internal static TimeSpan NextRunDelay(DateTimeOffset now)
    {
        var bugununCalismasi = new DateTimeOffset(now.Year, now.Month, now.Day, RunAtUtcHour, 0, 0, TimeSpan.Zero);
        var hedef = now < bugununCalismasi ? bugununCalismasi : bugununCalismasi.AddDays(1);
        return hedef - now;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var scrapers = scope.ServiceProvider.GetServices<IBrandScraper>()
            .Where(s => s.DailyOnly)
            .ToList();

        if (scrapers.Count == 0)
            return;

        var ingestion = scope.ServiceProvider.GetRequiredService<ScrapeIngestionService>();
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

        logger.LogInformation("Günlük tarama bitti.");
    }
}

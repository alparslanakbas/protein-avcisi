using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

public class DescriptionBackfillBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DescriptionBackfillBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("DescriptionBackfill:Enabled", true))
        {
            logger.LogInformation("Açıklama tamamlama devre dışı (DescriptionBackfill:Enabled=false).");
            return;
        }

        var intervalDays = configuration.GetValue("DescriptionBackfill:IntervalDays", 7);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(intervalDays));

        // DigestBackgroundService ile aynı desen — do-while DEĞİL, while. Her
        // deploy/restart'ta hemen tetiklenip marka sitelerine gereksiz istek
        // atılmasın diye ilk periyodun tamamen dolması bekleniyor.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<DescriptionBackfillService>();

            try
            {
                var updated = await backfill.BackfillAsync(stoppingToken);
                logger.LogInformation("Açıklama tamamlama çalıştı: {Count} ürün güncellendi.", updated);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Açıklama tamamlama sırasında hata oluştu.");
            }
        }
    }
}

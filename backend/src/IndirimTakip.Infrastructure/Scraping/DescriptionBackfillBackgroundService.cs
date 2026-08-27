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

        // Timer artık periyodun kendisini DEĞİL, yalnızca kontrol sıklığını
        // belirliyor; "sırası geldi mi" kararı DB'deki son çalışma damgasından
        // veriliyor (ProductDetailBackfillService.IsDueAsync).
        //
        // Öncesinde periyodu timer'ın kendisi tutuyordu ve bu, işin hiç
        // çalışmamasına yol açıyordu: her deploy/restart süreci sıfırdan
        // başlattığı için 7 günlük periyot bir kez bile dolmuyordu (bültende
        // aynı hata gerçekleşti, bkz. DigestBackgroundService). Durum DB'de
        // olduğundan başlangıçta hemen kontrol etmek de güvenli — aralık
        // dolmadıysa marka sitelerine hiç istek gitmiyor.
        var checkIntervalHours = configuration.GetValue("DescriptionBackfill:CheckIntervalHours", 6);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(checkIntervalHours));

        do
        {
            using var scope = scopeFactory.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<ProductDetailBackfillService>();

            try
            {
                if (!await backfill.IsDueAsync(intervalDays, stoppingToken))
                    continue;

                var updated = await backfill.BackfillAsync(stoppingToken);
                logger.LogInformation("Açıklama tamamlama çalıştı: {Count} ürün güncellendi.", updated);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Açıklama tamamlama sırasında hata oluştu.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

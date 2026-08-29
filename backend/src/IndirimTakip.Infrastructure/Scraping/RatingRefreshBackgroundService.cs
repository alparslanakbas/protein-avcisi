using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

// Markaların sitelerindeki yıldız ortalamasını düzenli olarak tazeler.
//
// Açıklama tamamlamadan farkı: orada "sırası geldi mi" diye bir aralık
// kontrolü var çünkü iş bir kez bitince tekrarlanmasına gerek yok. Puan ise
// sürekli değişen bir veri; burada her turda en eski kontrol edilen ürünler
// tazeleniyor, yani iş hiç "bitmiyor". Sıra RatingCheckedAt damgasından
// geldiği için zamanlama süreç belleğinde DEĞİL veritabanında — deploy'lar
// sırayı sıfırlamıyor (bültende tam olarak bu hata yaşanmıştı, bkz.
// DigestBackgroundService).
public class RatingRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RatingRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("RatingRefresh:Enabled", true))
        {
            logger.LogInformation("Puan tazeleme devre dışı (RatingRefresh:Enabled=false).");
            return;
        }

        var intervalHours = configuration.GetValue("RatingRefresh:IntervalHours", 6);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        do
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ProductRatingRefreshService>();

            try
            {
                await service.RefreshAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Puan tazeleme sırasında hata oluştu.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

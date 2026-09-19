using IndirimTakip.Core.Entities;
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
// DigestBackgroundService). ZAMANLAMA da artık öyle: eskiden her açılışta
// çalışıyordu, yani her deploy bütün kaynakların ürün sayfalarına bir tur istek
// gönderiyordu (bkz. PersistedSchedule).
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
        await PersistedSchedule.RunAsync(
            scopeFactory, BackgroundJobNames.PuanTazeleme, TimeSpan.FromHours(intervalHours), logger,
            async (services, cancellationToken) =>
            {
                try
                {
                    await services.GetRequiredService<ProductRatingRefreshService>()
                        .RefreshAsync(cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Başarısız tur da tur sayılıyor: hemen yeniden denemek aynı
                    // kaynaklara yine gitmek olurdu. Kapanış ise yukarı çıkıyor,
                    // böylece yarıda kalan tur eski damgayı koruyor.
                    logger.LogError(ex, "Puan tazeleme sırasında hata oluştu.");
                }
            },
            stoppingToken);
    }
}

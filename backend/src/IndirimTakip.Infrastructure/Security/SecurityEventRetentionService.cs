using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Security;

/// <summary>
/// Güvenlik olaylarını saklama süresi dolduğunda siliyor.
/// </summary>
/// <remarks>
/// <b>Neden zorunlu.</b> İki sebep var ve ikisi de tek başına yeterli:
/// (a) sınırsız büyüyen bir tablo, saldırı trafiği altında diskin kendisini
/// bir arıza kaynağına çevirir; (b) kişisel veri içeren bir kaydın süresiz
/// tutulması savunulabilir değil — saklama süresinin TANIMLI olması, kaydın
/// meşruiyetinin bir parçası.
///
/// <b>Neden durum tutmuyor.</b> Bu depoda aynı tuzağa iki kez düşüldü: periyodu
/// timer'ın kendisi tutunca her deploy süreci sıfırlıyor ve iş hiç
/// çalışmıyor (bülten ve detay tamamlama). Burada o sorun hiç doğmuyor,
/// çünkü silme İŞLEMİ ZATEN ETKİSİZ-TEKRARLANABİLİR: her turda "süresi
/// geçmişleri sil" demek yeterli, kaçırılan bir tur bir sonrakinde telafi
/// oluyor. Bu yüzden damga tutmaya gerek yok.
/// </remarks>
public class SecurityEventRetentionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SecurityEventRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retentionDays = configuration.GetValue("SecurityLog:RetentionDays", 90);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var esik = DateTimeOffset.UtcNow.AddDays(-retentionDays);
                var silinen = await db.SecurityEvents
                    .Where(x => x.OccurredAt < esik)
                    .ExecuteDeleteAsync(stoppingToken);

                if (silinen > 0)
                    logger.LogInformation("Güvenlik olayı temizliği: {Count} kayıt silindi ({Days} günden eski).", silinen, retentionDays);

                // Yönetim hatası kaydı AYNI süreyle temizleniyor. İki kaydı
                // farklı sürelerle tutmak, gizlilik metninde tek bir süre
                // yazarken kendi içinde çelişmek olurdu; ayrıca ikinci bir
                // ayar, unutulduğunda sınırsız büyüyen bir tablo demek.
                var yonetimSilinen = await db.AdminOperationFailures
                    .Where(x => x.OccurredAt < esik)
                    .ExecuteDeleteAsync(stoppingToken);

                if (yonetimSilinen > 0)
                    logger.LogInformation("Yönetim hatası temizliği: {Count} kayıt silindi ({Days} günden eski).", yonetimSilinen, retentionDays);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Güvenlik olayı temizliği başarısız.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

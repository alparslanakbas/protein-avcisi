using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Subscribers;

public class DigestBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DigestBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Digest:Enabled", true))
        {
            logger.LogInformation("Zamanlanmış bülten gönderimi devre dışı (Digest:Enabled=false).");
            return;
        }

        // Zamanlamayı ARTIK bu timer belirlemiyor; sadece "kontrol etme"
        // sıklığı. Gerçek karar (hangi abonenin maili zamanı geldi) her turda
        // DB'deki Subscriber.LastDigestSentAt'e bakılarak veriliyor.
        //
        // Öncesinde timer'ın kendisi 7 günlük periyodu tutuyordu ve bu, bülten
        // hiç gönderilememesine yol açıyordu: her deploy/restart süreci
        // sıfırdan başlattığı için 7 günlük periyot bir kez bile dolmuyordu.
        // Durum artık DB'de olduğundan restart zamanlamayı etkilemiyor, aynı
        // sebeple başlangıçta hemen bir kontrol yapmak da güvenli (gönderim
        // için hâlâ aboneye özel 7 günün dolması gerekiyor, yani deploy başına
        // tekrar mail gitmiyor).
        var checkIntervalHours = configuration.GetValue("Digest:CheckIntervalHours", 6);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(checkIntervalHours));

        do
        {
            await SendDigestAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDigestAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var digest = scope.ServiceProvider.GetRequiredService<DigestService>();
        var baseUrl = configuration["PublicBaseUrl"] ?? "http://localhost:5156";

        try
        {
            var result = await digest.SendDigestAsync(baseUrl, cancellationToken);
            if (result.SubscriberCount > 0 || result.PendingCount > 0)
            {
                logger.LogInformation(
                    "Zamanlanmış bülten: {DealCount} ürün, {SubscriberCount} aboneye gönderildi, {PendingCount} abone sıradaki tura kaldı.",
                    result.DealCount, result.SubscriberCount, result.PendingCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zamanlanmış bülten gönderimi başarısız oldu.");
        }
    }
}

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

        var intervalDays = configuration.GetValue("Digest:IntervalDays", 7);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(intervalDays));

        // ScrapingBackgroundService'in aksine BAŞLANGIÇTA hemen göndermiyoruz
        // (do-while değil, while) — bu projede deploy/restart sık olduğu için
        // her yeniden başlatmada abonelere bülten gitmesin diye ilk periyodun
        // tamamen dolmasını bekliyoruz.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SendDigestAsync(stoppingToken);
        }
    }

    private async Task SendDigestAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var digest = scope.ServiceProvider.GetRequiredService<DigestService>();
        var baseUrl = configuration["PublicBaseUrl"] ?? "http://localhost:5156";

        try
        {
            var result = await digest.SendDigestAsync(baseUrl, cancellationToken);
            logger.LogInformation(
                "Zamanlanmış bülten gönderildi: {DealCount} ürün, {SubscriberCount} abone.",
                result.DealCount, result.SubscriberCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zamanlanmış bülten gönderimi başarısız oldu.");
        }
    }
}

using System.Net;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Shopify istemcilerinin işleyici zincirinde: doğrudan istek 429 alırsa aynı isteği
/// <see cref="ShopifyTuneli"/>'nden tekrarlar (gerekçe orada).
/// </summary>
/// <remarks>
/// Tünel çalışmazsa çekici doğrudan yanıtı (429) görür; yani en kötü durumda davranış tünelden
/// önceki gibi. HttpClient günlüğünde tünelin izi: iç satır "Received ... - 429", dış satır
/// "End processing ... - 200".
/// </remarks>
public sealed class ShopifyTunelIsleyicisi(ShopifyTuneli tunel, ILogger<ShopifyTunelIsleyicisi> logger)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Gövdeli istek kopyalanamıyor; Shopify istekleri zaten GET.
        if (!tunel.Etkin || request.Content is not null)
            return await base.SendAsync(request, cancellationToken);

        if (tunel.TuneldenGitmeli)
            return await TuneldenAsync(request, cancellationToken)
                ?? await base.SendAsync(request, cancellationToken);

        var dogrudan = await base.SendAsync(request, cancellationToken);
        if (dogrudan.StatusCode != HttpStatusCode.TooManyRequests)
            return dogrudan;

        if (tunel.EngelGoruldu())
            logger.LogInformation("Shopify {Host} 429 verdi; Shopify istekleri {Dakika} dakika ev tünelinden gidecek.",
                request.RequestUri?.Host, ShopifyTuneli.YapiskanSure.TotalMinutes);

        var tuneldenGelen = await TuneldenAsync(request, cancellationToken);
        if (tuneldenGelen is null)
            return dogrudan;

        dogrudan.Dispose();
        return tuneldenGelen;
    }

    /// <summary>Tünelden gelen yanıt; tünel çalışmadıysa null (çağıran doğrudan yola döner).</summary>
    private async Task<HttpResponseMessage?> TuneldenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await tunel.GonderAsync(Kopyala(request), cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException)
                                   && !cancellationToken.IsCancellationRequested)
        {
            // Sonraki istekler yine önce doğrudan denensin; tünel dönünce bir sonraki 429 onu yeniden açar.
            tunel.Sifirla();
            logger.LogWarning(ex, "Shopify tüneli çalışmadı ({Host}); istek doğrudan yoldan sürüyor.",
                request.RequestUri?.Host);
            return null;
        }
    }

    // Bir istek iki kez gönderilemiyor; tünele kopyası gidiyor. Başlıklar (User-Agent dahil)
    // HttpClient'ın varsayılan başlıkları eklendikten sonra buraya geliyor.
    private static HttpRequestMessage Kopyala(HttpRequestMessage istek)
    {
        var kopya = new HttpRequestMessage(istek.Method, istek.RequestUri)
        {
            Version = istek.Version,
            VersionPolicy = istek.VersionPolicy,
        };
        foreach (var (ad, degerler) in istek.Headers)
            kopya.Headers.TryAddWithoutValidation(ad, degerler);
        return kopya;
    }
}

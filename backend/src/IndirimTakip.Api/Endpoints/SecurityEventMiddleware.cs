using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Security;

namespace IndirimTakip.Api.Endpoints;

internal static class SecurityEventMiddleware
{
    /// <summary>
    /// Dikkate değer istekleri (yetkisiz deneme, hız sınırı, açık taraması,
    /// sunucu hatası) veritabanına kaydeder.
    /// </summary>
    /// <remarks>
    /// Boru hattında ERKEN duruyor ama kayıt <c>await next()</c> SONRASINDA
    /// yapılıyor: karar durum koduna bakıyor ve durum kodu ancak aşağıdaki
    /// katmanlar çalıştıktan sonra kesinleşiyor. Erken durması gerekiyor ki
    /// hız sınırlayıcının ürettiği 429'ları da görebilsin.
    /// </remarks>
    public static IApplicationBuilder UseSecurityEventLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            await next();

            var path = context.Request.Path.Value ?? string.Empty;
            var kind = SecurityEventClassifier.Classify(context.Response.StatusCode, path);
            if (kind is null)
                return;

            var recorder = context.RequestServices.GetService<SecurityEventRecorder>();
            if (recorder is null)
                return;

            var olay = new SecurityEvent
            {
                OccurredAt = DateTimeOffset.UtcNow,
                Ip = RequestLoggingExtensions.GetClientIp(context),
                Kind = kind,
                Method = context.Request.Method,
                Path = Kirp(path, 500) ?? "/",
                StatusCode = context.Response.StatusCode,
                UserAgent = Kirp(context.Request.Headers.UserAgent.FirstOrDefault(), 500),
                Country = Kirp(context.Request.Headers["CF-IPCountry"].FirstOrDefault(), 2),
            };

            // İPTAL JETONU BİLEREK VERİLMİYOR (CancellationToken.None).
            // context.RequestAborted kullanılsaydı, bağlantısını kasten yarıda
            // kesen bir saldırgan kendi kaydının yazılmasını engelleyebilirdi —
            // yani kaydı atlatmanın yolu, isteği bırakmak olurdu.
            await recorder.RecordAsync(olay, CancellationToken.None);
        });
    }

    // Sorgu dizesi BİLEREK saklanmıyor: adres yolunun kendisi olayı tanımlamaya
    // yetiyor, sorgu ise kullanıcı e-postası gibi konuyla ilgisiz kişisel veri
    // taşıyabiliyor. Dar tutmak kaydın meşruiyetinin parçası.
    private static string? Kirp(string? deger, int enFazla)
    {
        if (string.IsNullOrEmpty(deger))
            return null;

        return deger.Length <= enFazla ? deger : deger[..enFazla];
    }
}

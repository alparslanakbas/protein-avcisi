using Microsoft.AspNetCore.DataProtection;

namespace IndirimTakip.Api.Endpoints;


internal static class AdminAuthExtensions
{
    /// <summary>
    /// Admin uçlarını korur: ya <c>X-Admin-Key</c> başlığı ya da yönetim
    /// panelinin HttpOnly oturum çerezi.
    /// </summary>
    /// <remarks>
    /// Çerez yolu 6 Eylül'de eklendi. Alternatifi, panelin admin anahtarını
    /// tarayıcıda tutup her istekte başlık olarak göndermesiydi — o anahtar
    /// bütün abonelere e-posta gönderebildiği için JavaScript'in erişebildiği
    /// bir yerde durmamalı. Başlık yolu KALDIRILMADI: betikler, cron ve elle
    /// yapılan çağrılar onu kullanıyor.
    /// </remarks>
    public static RouteHandlerBuilder RequireAdminKey(this RouteHandlerBuilder builder, string? expectedKey)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            if (string.IsNullOrEmpty(expectedKey))
                return Results.Unauthorized();

            var providedKey = context.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault();
            if (providedKey == expectedKey)
                return await next(context);

            var dataProtection = context.HttpContext.RequestServices.GetService<IDataProtectionProvider>();
            if (dataProtection is not null
                && YonetimSessionEndpoints.GecerliOturum(context.HttpContext, dataProtection))
            {
                return await next(context);
            }

            // Cloudflare Access'in imzalı kimlik jetonu. Access zaten kimliği
            // doğrulayıp bunu isteğe ekliyor; jetonu doğrulamak, elle girilen
            // bir anahtarı kabul etmekten daha sağlam. Yapılandırılmamışsa bu
            // yol tamamen kapalı (bkz. CloudflareAccessValidator).
            var access = context.HttpContext.RequestServices.GetService<CloudflareAccessValidator>();
            if (access is not null
                && await access.GecerliMi(context.HttpContext, context.HttpContext.RequestAborted))
            {
                return await next(context);
            }

            return Results.Unauthorized();
        });
    }
}

// 2026-08-15 güvenlik olayı sonrası eklendi: e-posta gönderen/yazma yapan
// uçlarda hiç istek logu yoktu, kötüye kullanım olduğunda Render loglarında
// hiçbir iz kalmıyordu. IP + yöntem + yol + zaman `app.Logger` üzerinden
// (Render'ın stdout'u yakaladığı standart kanal) logluyor — ayrı bir log
// servisi/DB tablosu kurmak burada aşırı mühendislik olurdu.
internal static class RequestLoggingExtensions
{
    public static RouteHandlerBuilder LogSensitiveRequest(this RouteHandlerBuilder builder, ILogger logger)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var ip = GetClientIp(context.HttpContext);
            logger.LogInformation("Hassas istek: {Ip} {Method} {Path}",
                ip, context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return await next(context);
        });
    }

    // 2026-08-15: Render + Cloudflare çift proxy zincirinde RemoteIpAddress
    // (ForwardedHeaders middleware'den sonra bile) Render'ın kendi iç ağındaki
    // bir IP'yi döndürüyordu (10.x.x.x), gerçek ziyaretçi IP'si kayboluyordu —
    // bu da rate limiter'ın ve istek loglarının işe yaramamasına yol açıyordu
    // (tüm istekler aynı "IP" gibi görünüp ortak bir limiti paylaşıyordu).
    // Cloudflare'in CF-Connecting-IP header'ı tam bunun için var — Cloudflare
    // bunu kendi edge'inde üretip origin'e gönderiyor, dışarıdan sahtesi
    // yazılamaz (Cloudflare kendi değerini her zaman ezer). Cloudflare
    // arkasında değilsek (yerel geliştirme) normal RemoteIpAddress'e düşer.
    public static string GetClientIp(HttpContext context)
    {
        var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        return !string.IsNullOrEmpty(cfConnectingIp)
            ? cfConnectingIp
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

record VoteRequest(bool Helpful);
record RecoverFavoritesRequest(string Email);

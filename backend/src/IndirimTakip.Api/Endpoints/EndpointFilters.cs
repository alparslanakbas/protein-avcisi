using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

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
            if (!await YetkiliMi(context.HttpContext, expectedKey))
                return Results.Unauthorized();

            return await KaydederekCalistir(context, next);
        });
    }

    private static async Task<bool> YetkiliMi(HttpContext http, string? expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey))
            return false;

        var providedKey = http.Request.Headers["X-Admin-Key"].FirstOrDefault();
        if (providedKey == expectedKey)
            return true;

        var dataProtection = http.RequestServices.GetService<IDataProtectionProvider>();
        if (dataProtection is not null
            && YonetimSessionEndpoints.GecerliOturum(http, dataProtection))
        {
            return true;
        }

        // Cloudflare Access'in imzalı kimlik jetonu. Access zaten kimliği
        // doğrulayıp bunu isteğe ekliyor; jetonu doğrulamak, elle girilen
        // bir anahtarı kabul etmekten daha sağlam. Yapılandırılmamışsa bu
        // yol tamamen kapalı (bkz. CloudflareAccessValidator).
        var access = http.RequestServices.GetService<CloudflareAccessValidator>();
        return access is not null
            && await access.GecerliMi(http, http.RequestAborted);
    }

    /// <summary>
    /// Ucu çalıştırır; başarısız olursa SEBEBİNİ kaydeder.
    /// </summary>
    /// <remarks>
    /// <b>NEDEN BURASI.</b> Bu filtre bütün yönetim uçlarının ortak geçidi;
    /// kaydı buraya koymak, her uca tek tek eklemeye kıyasla hem tek yerde
    /// duruyor hem de bundan sonra eklenen uçlar için kendiliğinden çalışıyor.
    ///
    /// <b>YANIT GÖVDESİ OKUNMUYOR, DÖNEN SONUÇ NESNESİ OKUNUYOR.</b>
    /// Alternatif, yanıt akışını tampona alıp gövdeyi ayrıştırmaktı; her
    /// istekte kopyalama demek olurdu ve <c>Results.NotFound("mesaj")</c>
    /// nesnesi mesajı zaten yapısal olarak taşıyor.
    ///
    /// <b>YETKİSİZ DENEMELER BURAYA GİRMİYOR</b> — bu noktaya yalnızca
    /// kimliği doğrulanmış istek ulaşıyor. 401'ler zaten SecurityEvents'te
    /// ve oraya ait: onlar yönetim hatası değil, dışarıdan gelen deneme.
    /// </remarks>
    private static async ValueTask<object?> KaydederekCalistir(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        object? sonuc;
        try
        {
            sonuc = await next(context);
        }
        catch (Exception ex)
        {
            // İstemci bağlantıyı kestiyse bu bir arıza değil; kaydetmek
            // paneli gürültüyle doldururdu.
            if (ex is not OperationCanceledException || !context.HttpContext.RequestAborted.IsCancellationRequested)
                await Kaydet(context.HttpContext, StatusCodes.Status500InternalServerError, AdminFailureReason.Istisnadan(ex));

            // Davranış DEĞİŞMİYOR: istisna olduğu gibi yukarı gidiyor.
            throw;
        }

        if (sonuc is IStatusCodeHttpResult { StatusCode: >= 400 } durum)
            await Kaydet(context.HttpContext, durum.StatusCode!.Value, MesajCikar(sonuc));

        return sonuc;
    }

    private static async Task Kaydet(HttpContext http, int durumKodu, string? sebep)
    {
        var recorder = http.RequestServices.GetService<AdminFailureRecorder>();
        if (recorder is null)
            return;

        var kayit = new AdminOperationFailure
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Method = http.Request.Method,
            // Sorgu dizesi SecurityEvents'teki gerekçeyle burada da
            // saklanmıyor: yol olayı tanımlamaya yetiyor.
            Path = Yol(http.Request.Path.Value),
            StatusCode = durumKodu,
            Reason = sebep,
            Ip = RequestLoggingExtensions.GetClientIp(http),
        };

        await recorder.RecordAsync(kayit, CancellationToken.None);
    }

    /// <summary>Sonuç nesnesinin taşıdığı hata metni; yoksa null.</summary>
    private static string? MesajCikar(object? sonuc)
    {
        if (sonuc is not IValueHttpResult deger)
            return null;

        // ProblemDetails BURADA çözülüyor, AdminFailureReason'da değil: o tip
        // ASP.NET'e ait, sebep üretimi ise Infrastructure'da duruyor — yani
        // test projesinin görebildiği yerde.
        return deger.Value is ProblemDetails problem
            ? AdminFailureReason.Degerden(problem.Detail ?? problem.Title)
            : AdminFailureReason.Degerden(deger.Value);
    }

    private static string Yol(string? yol)
    {
        if (string.IsNullOrEmpty(yol))
            return "/";

        return yol.Length <= 500 ? yol : yol[..500];
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

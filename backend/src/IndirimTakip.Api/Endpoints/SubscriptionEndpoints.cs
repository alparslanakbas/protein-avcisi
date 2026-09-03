using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Api.Endpoints;

// Bülten aboneliği: kayıt, e-posta onayı ve çıkış.
internal static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this WebApplication app, string frontendBaseUrl)
    {
        // E-posta bülteni: double opt-in zorunlu (İYS/KVKK gereği) — bu endpoint
        // hiçbir aboneyi doğrudan aktifleştirmiyor, sadece onay maili tetikliyor.
        app.MapPost("/api/subscribe", async (SubscribeRequest request, SubscriberService subscribers,
            EmailAddressValidator emailValidator, HttpContext http, CancellationToken ct) =>
        {
            // Bal küpü dolu geldiyse istek bir bot tarafından yapılmış demektir.
            // Hata döndürmüyoruz: bot hangi ölçütte elendiğini öğrenmemeli, ayrıca
            // gerçek bir kullanıcı bu dalı hiç görmüyor.
            if (!string.IsNullOrWhiteSpace(request.Website))
            {
                app.Logger.LogInformation("Bal küpü doldurulmuş abonelik isteği yok sayıldı: {Ip}",
                    RequestLoggingExtensions.GetClientIp(http));
                return Results.Ok(new { message = "E-postanı kontrol et, onay bağlantısı gönderdik." });
            }

            if (!EndpointHelpers.IsValidEmail(request.Email))
                return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

            // Alan adı gerçekten var mı: uydurma adreslere onay postası göndermek hem
            // kotadan yiyor hem geri dönen postalar gönderen itibarını düşürüyor.
            if (!await emailValidator.IsDeliverableAsync(request.Email, ct))
                return Results.BadRequest(new { message = "Bu e-posta adresine ulaşılamıyor, kontrol eder misin?" });

            var confirmBaseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            var sent = await subscribers.SubscribeAsync(request, confirmBaseUrl, ct);
            if (!sent)
                return Results.Json(new { message = "Onay e-postası şu anda gönderilemiyor, lütfen birazdan tekrar dene." }, statusCode: StatusCodes.Status502BadGateway);
            return Results.Ok(new { message = "E-postanı kontrol et, onay bağlantısı gönderdik." });
        }).RequireRateLimiting("EmailSensitive").LogSensitiveRequest(app.Logger);

        // Onay/abonelikten çıkma linkleri e-postadan doğrudan tıklanıyor, bu yüzden
        // JSON değil basit bir HTML sayfası dönüyor — ayrı bir frontend route'u
        // kurmak bu iki statik mesaj için gereksiz olurdu. charset=utf-8 elle
        // belirtilmezse tarayıcı Türkçe karakterleri bozuk gösterebiliyor.
        app.MapGet("/api/subscribe/confirm/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
        {
            var success = await subscribers.ConfirmAsync(token, ct);
            var html = success
                ? EndpointHelpers.BuildSubscriptionConfirmedPage(frontendBaseUrl)
                : EndpointHelpers.BuildInfoPage("Bu bağlantı geçersiz.", "Onay linki süresi geçmiş ya da daha önce kullanılmış olabilir.", frontendBaseUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapGet("/api/subscribe/unsubscribe/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
        {
            var success = await subscribers.UnsubscribeAsync(token, ct);
            var html = success
                ? EndpointHelpers.BuildInfoPage("Bültenden çıkarıldın.", "Fikrini değiştirirsen tekrar abone olabilirsin.", frontendBaseUrl)
                : EndpointHelpers.BuildInfoPage("Bu bağlantı geçersiz.", "Bağlantı süresi geçmiş ya da daha önce kullanılmış olabilir.", frontendBaseUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        });
    }
}

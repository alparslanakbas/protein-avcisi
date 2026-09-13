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
        //
        // GET DURUMU DEĞİŞTİRMEZ. GET eskiden doğrudan onaylıyordu; aynı kodu
        // taşıyan ABD sitesinde Gmail'in bağlantı tarayıcısı bir test aboneliğini
        // alıcı tıklamadan bir saniye önce kendisi onayladı (13 Eylül) — yani
        // çift onay hiçbir şey kanıtlamıyordu. GET artık düğmeli sayfa gösteriyor,
        // onayı ya da çıkışı yalnızca o düğmenin gönderdiği POST yapıyor.
        // Çıkış da aynı kurala bağlı; yoksa tarayıcılar bülten altbilgisindeki
        // bağlantıyı açıp gerçek aboneleri sessizce listeden düşürürdü.
        const string GecersizBaslik = "Bu bağlantı geçersiz.";
        const string GecersizMesaj = "Bağlantı süresi geçmiş ya da daha önce kullanılmış olabilir.";

        app.MapGet("/api/subscribe/confirm/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
        {
            var html = await subscribers.TokenExistsAsync(token, ct)
                ? EndpointHelpers.BuildActionPage("Aboneliğini onayla", "Tek tıkla haftanın gerçek fiyat düşüşleri e-postana gelmeye başlasın.",
                    "Aboneliğimi onayla", $"/api/subscribe/confirm/{Uri.EscapeDataString(token)}", frontendBaseUrl)
                : EndpointHelpers.BuildInfoPage(GecersizBaslik, GecersizMesaj, frontendBaseUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapPost("/api/subscribe/confirm/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
        {
            var success = await subscribers.ConfirmAsync(token, ct);
            var html = success
                ? EndpointHelpers.BuildSubscriptionConfirmedPage(frontendBaseUrl)
                : EndpointHelpers.BuildInfoPage(GecersizBaslik, GecersizMesaj, frontendBaseUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapGet("/api/subscribe/unsubscribe/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
        {
            var html = await subscribers.TokenExistsAsync(token, ct)
                ? EndpointHelpers.BuildActionPage("Bültenden çıkmak istiyor musun?", "Haftalık fiyat düşüşleri artık gelmeyecek. İstediğin zaman tekrar abone olabilirsin.",
                    "Bültenden çık", $"/api/subscribe/unsubscribe/{Uri.EscapeDataString(token)}", frontendBaseUrl)
                : EndpointHelpers.BuildInfoPage(GecersizBaslik, GecersizMesaj, frontendBaseUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapPost("/api/subscribe/unsubscribe/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
        {
            var success = await subscribers.UnsubscribeAsync(token, ct);
            var html = success
                ? EndpointHelpers.BuildInfoPage("Bültenden çıkarıldın.", "Fikrini değiştirirsen tekrar abone olabilirsin.", frontendBaseUrl)
                : EndpointHelpers.BuildInfoPage(GecersizBaslik, GecersizMesaj, frontendBaseUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        });
    }
}

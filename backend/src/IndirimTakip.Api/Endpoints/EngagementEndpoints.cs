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

// Ziyaretçi etkileşimi: takip listesi, favoriler, tıklama sayacı ve
// "faydalı mı" oyları. Hepsi hız sınırına tabi.
internal static class EngagementEndpoints
{
    public static void MapEngagementEndpoints(this WebApplication app, string frontendBaseUrl)
    {
        // "Haber Ver" — bir sonraki taramada bu ürünün fiyatı gerçekten düşerse
        // tek seferlik bir bildirim e-postası gönderiliyor (bkz. ProductWatchNotifier).
        app.MapPost("/api/products/{id:int}/watch", async (int id, WatchProductRequest request, ProductWatchService watchService, HttpContext http, CancellationToken ct) =>
        {
            if (!EndpointHelpers.IsValidEmail(request.Email))
                return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

            var confirmBaseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            var success = await watchService.WatchAsync(id, request, confirmBaseUrl, ct);
            return success ? Results.Ok(new { message = "Fiyat düşünce sana haber vereceğiz." }) : Results.NotFound();
        }).RequireRateLimiting("EmailSensitive").LogSensitiveRequest(app.Logger);

        // Favoriler ("listem") — hesap/login gerektirmiyor. İlk ekleme e-posta ile
        // yapılır, dönen token tarayıcıda saklanıp sonraki isteklerde kullanılır.
        // Haber Ver'in aksine hiç e-posta gönderilmiyor, bu yüzden onay akışına
        // hiç girmiyor.
        app.MapPost("/api/products/{id:int}/favorite", async (int id, FavoriteRequest request, FavoriteService favorites, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.Token) && !EndpointHelpers.IsValidEmail(request.Email))
                return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

            var (success, token, recoverySent) = await favorites.AddAsync(id, request.Token, request.Email, frontendBaseUrl, ct);
            return success ? Results.Ok(new { token, recoverySent }) : Results.NotFound();
        }).RequireRateLimiting("EmailSensitive").LogSensitiveRequest(app.Logger);

        app.MapDelete("/api/products/{id:int}/favorite", async (int id, string token, FavoriteService favorites, CancellationToken ct) =>
        {
            var removed = await favorites.RemoveAsync(id, token, ct);
            return removed ? Results.Ok() : Results.NotFound();
        }).RequireRateLimiting("General").LogSensitiveRequest(app.Logger);

        app.MapGet("/api/favorites", async (string token, FavoriteService favorites, DealsQueryService deals, CancellationToken ct) =>
        {
            var productIds = await favorites.GetFavoriteProductIdsAsync(token, ct);
            if (productIds is null)
                return Results.NotFound();

            var result = await deals.GetDealsByIdsAsync(productIds, cancellationToken: ct);
            return Results.Ok(result);
        });

        // Favori listesi kurtarma — localStorage token'ı kaybolan kullanıcı (farklı
        // cihaz/tarayıcı, temizlenen site verisi vb. — gerçek bir kullanıcı raporuyla
        // fark edildi) e-postasını girip token'ı içeren bir linki e-postasına
        // alabiliyor. Email enumeration'ı önlemek için (bkz. 2026-08-15 token ifşası
        // düzeltmesiyle aynı gerekçe) yanıt e-postanın kayıtlı olup olmadığından
        // bağımsız hep aynı — sadece format geçersizse ayırt edici bir hata dönüyoruz.
        app.MapPost("/api/favorites/recover", async (RecoverFavoritesRequest request, FavoriteService favorites, CancellationToken ct) =>
        {
            if (!EndpointHelpers.IsValidEmail(request.Email))
                return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

            await favorites.SendRecoveryEmailAsync(request.Email, frontendBaseUrl, ct);
            return Results.Ok(new { message = "Bu e-posta kayıtlıysa favori linkini gönderdik." });
        }).RequireRateLimiting("EmailSensitive").LogSensitiveRequest(app.Logger);

        // Mağaza tıklaması. Bağlantı artık DOĞRUDAN mağazaya gittiği için (bkz.
        // DealDto.StoreUrl) sayacı /go/{id} artıramıyor; tıklama anında buraya
        // beacon gönderiliyor.
        //
        // Yan fayda: sayaç artık yalnızca JavaScript çalıştıran gerçek tarayıcılarda
        // artıyor. /go'da bunun için ayrıca user-agent'a bakıp bot ayıklamak
        // gerekiyordu (sayaç markalarla paylaşılan tıklama raporunu besliyor ve bot
        // trafiğiyle şişerse veri doğrudan yanıltıcı olur).
        //
        // Gövde beklenmiyor: navigator.sendBeacon boş gövdeyle çağrılıyor ki istek
        // "basit" kalsın ve CORS ön kontrolü tetiklenmesin — ön kontrol, sayfa
        // mağazaya giderken iptal edilip sayaç kaybolabilirdi.
        app.MapPost("/api/products/{id:int}/click", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            // Tek deyimde artırma: ürünü belleğe çekip SaveChanges yapmaya gerek yok
            // ve eşzamanlı tıklamalarda kayıp güncelleme riski kalmıyor.
            var affected = await db.Products
                .Where(p => p.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ClickCount, p => p.ClickCount + 1), ct);

            return affected == 0 ? Results.NotFound() : Results.NoContent();
        }).RequireRateLimiting("General");

        // "Bu bilgi faydalı mıydı?" oyu — basit güven sinyali, /go ile aynı desende
        // (auth yok, kim oy verdiğini takip etmiyoruz — tekrar oy vermeyi frontend
        // localStorage ile engelliyor, backend'de dedup gerekmiyor).
        app.MapPost("/api/products/{id:int}/vote", async (int id, VoteRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null)
                return Results.NotFound();

            if (request.Helpful)
                product.HelpfulYesCount++;
            else
                product.HelpfulNoCount++;

            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).RequireRateLimiting("General");
    }
}

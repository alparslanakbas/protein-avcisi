using IndirimTakip.Core.Caching;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Catalog;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Api.Endpoints;

// Yönetim panelinin ABONE uçları: bülten gönderimi, abone listesi ve işlemleri,
// e-posta kapasitesi.
// AdminEndpoints.cs'ten panel sekmelerine göre ayrıldı (güvenlik/mimari incelemesi,
// 26 Eylül); uçlar birebir aynı, hepsi X-Admin-Key ile korunuyor.
internal static class AdminAboneEndpoints
{
    public static void MapAdminAboneEndpoints(this WebApplication app, string? adminApiKey)
    {
        // Asıl gönderim artık DigestBackgroundService ile haftada bir otomatik
        // tetikleniyor — bu endpoint elle/anlık test tetiklemesi için hâlâ duruyor
        // (aynı /api/dev/ingest deseninde, BackgroundService eklendikten sonra da).
        app.MapPost("/api/dev/send-digest", async (DigestService digest, IConfiguration config, CancellationToken ct) =>
        {
            // Zamanlanmış gönderimle aynı adres. Host'tan kurulunca panel
            // yolundan (www) tetiklenen bültende "listeden çık" bağlantıları
            // frontend'e düşüp 404 veriyordu (güvenlik incelemesi, 25 Eylül).
            var baseUrl = (config["PublicBaseUrl"] ?? "https://api.proteinavcisi.com.tr").TrimEnd('/');
            var result = await digest.SendDigestAsync(baseUrl, ct);
            return Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // --- Bulten aboneleri (yonetim paneli) ---
        //
        // OZET LISTEDEN AYRI SAYILIYOR (guvenlik olaylarindaki gerekceyle
        // ayni): liste 1000 satirla sinirli, kirpilmis listeden sayi cikarmak
        // toplami eksik gosterirdi.
        app.MapGet("/api/dev/aboneler", async (AppDbContext db, CancellationToken ct) =>
        {
            var satirlar = await db.Subscribers
                .AsNoTracking()
                .OrderByDescending(s => s.SubscribedAt)
                .Take(1000)
                .Select(s => new
                {
                    s.Id,
                    s.Email,
                    s.IsConfirmed,
                    s.SubscribedAt,
                    s.ConfirmedAt,
                    s.UnsubscribedAt,
                    s.LastConfirmationEmailSentAt,
                    s.LastDigestSentAt,
                    // Ayni tablo fiyat alarmini ve takip listesini de tasiyor;
                    // pasife almanin neyi etkileyecegini gosteriyor.
                    takipSayisi = db.ProductWatches.Count(w => w.SubscriberId == s.Id),
                    favoriSayisi = db.ProductFavorites.Count(f => f.SubscriberId == s.Id),
                })
                .ToListAsync(ct);

            var aboneler = satirlar.Select(s => new
            {
                s.Id,
                s.Email,
                durum = SubscriberService.StatusOf(s.IsConfirmed, s.UnsubscribedAt) switch
                {
                    SubscriberStatus.Active => "aktif",
                    SubscriberStatus.Pending => "bekliyor",
                    _ => "ayrildi",
                },
                s.SubscribedAt,
                s.ConfirmedAt,
                s.UnsubscribedAt,
                s.LastConfirmationEmailSentAt,
                s.LastDigestSentAt,
                s.takipSayisi,
                s.favoriSayisi,
            });

            var ozet = await db.Subscribers
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    toplam = g.Count(),
                    aktif = g.Count(x => x.IsConfirmed && x.UnsubscribedAt == null),
                    bekleyen = g.Count(x => !x.IsConfirmed && x.UnsubscribedAt == null),
                    ayrilan = g.Count(x => x.UnsubscribedAt != null),
                })
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new
            {
                aboneler,
                ozet = ozet ?? new { toplam = 0, aktif = 0, bekleyen = 0, ayrilan = 0 },
            });
        }).RequireAdminKey(adminApiKey);

        app.MapPost("/api/dev/aboneler/{id:int}/pasife-al", async (int id, SubscriberService aboneler, CancellationToken ct) =>
            await aboneler.DeactivateAsync(id, ct)
                ? Results.Ok(new { id, durum = "ayrildi" })
                : Results.NotFound($"{id} numarali abone bulunamadi.")).RequireAdminKey(adminApiKey);

        app.MapPost("/api/dev/aboneler/{id:int}/onay-gonder", async (
            int id, SubscriberService aboneler, IConfiguration config, CancellationToken ct) =>
        {
            // ISTEGIN ADRESI DEGIL: panel bu uca www.proteinavcisi.com.tr/yonetim/api
            // uzerinden geliyor ve www, /api/subscribe yolunu backend'e tasimiyor;
            // oradan kurulan onay baglantisi cikmaz sokak olurdu.
            var confirmBaseUrl = (config["PublicBaseUrl"] ?? "https://api.proteinavcisi.com.tr").TrimEnd('/');

            return await aboneler.ResendConfirmationAsync(id, confirmBaseUrl, ct) switch
            {
                AdminConfirmationResult.Sent => Results.Ok(new { message = "Onay e-postası gönderildi." }),
                AdminConfirmationResult.NotFound => Results.NotFound($"{id} numaralı abone bulunamadı."),
                AdminConfirmationResult.AlreadyActive => Results.Conflict("Bu abone zaten aktif."),
                AdminConfirmationResult.CoolingDown => Results.Json(
                    new { message = "Son 5 dakika içinde zaten bir onay e-postası gönderildi. Biraz sonra tekrar dene." },
                    statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.Json(
                    new { message = "Onay e-postası şu anda gönderilemedi; e-posta sağlayıcısı yanıt vermedi." },
                    statusCode: StatusCodes.Status502BadGateway),
            };
        }).RequireAdminKey(adminApiKey);

        // E-posta kapasitesi raporu. Sağlayıcının günlük kotası bültenle transactional
        // mailler (onay, fiyat alarmı, favori kurtarma) arasında paylaşıldığı için,
        // kota sessizce dolduğunda yeni bir abone onay mailini hiç alamaz — dışarıdan
        // hiçbir hata görünmeden. Bu uç nokta, o sınıra ne kadar kaldığını görünür
        // kılıyor; buradaki "kalan gün" tahmini abone sayısı arttıkça takip edilmeli.
        app.MapGet("/api/dev/email-stats", async (AppDbContext db, IConfiguration config, CancellationToken ct) =>
        {
            var intervalDays = config.GetValue("Digest:IntervalDays", 7);
            var dailyQuota = config.GetValue("Digest:DailyQuota", 200);
            var now = DateTimeOffset.UtcNow;
            var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            var dueBefore = now.AddDays(-intervalDays);

            var activeSubscribers = await db.Subscribers
                .CountAsync(s => s.IsConfirmed && s.UnsubscribedAt == null, ct);
            var pendingConfirmation = await db.Subscribers
                .CountAsync(s => !s.IsConfirmed && s.UnsubscribedAt == null, ct);
            var sentToday = await db.Subscribers
                .CountAsync(s => s.LastDigestSentAt >= todayStart, ct);
            var awaitingDigest = await db.Subscribers
                .CountAsync(s => s.IsConfirmed && s.UnsubscribedAt == null
                    && (s.LastDigestSentAt == null || s.LastDigestSentAt < dueBefore), ct);

            // Bir bülten turunun kaç güne yayıldığı: kota tavanı aşıldığında kalanlar
            // ertesi güne devrediyor (bkz. DigestService).
            var daysPerRound = (int)Math.Ceiling(activeSubscribers / (double)dailyQuota);

            return Results.Ok(new
            {
                activeSubscribers,
                pendingConfirmation,
                digestIntervalDays = intervalDays,
                dailyDigestQuota = dailyQuota,
                sentToday,
                remainingQuotaToday = Math.Max(0, dailyQuota - sentToday),
                awaitingDigest,
                daysPerRound,
                // Bülten turu, gönderim aralığından uzun sürmeye başladığında abonelerin
                // bir kısmı o turu kaçırmaya başlar — pratik tavan bu.
                maxSubscribersAtCurrentSettings = dailyQuota * intervalDays,
                capacityUsedPercent = Math.Round(activeSubscribers * 100.0 / (dailyQuota * intervalDays), 2),
            });
        }).RequireAdminKey(adminApiKey);
    }
}

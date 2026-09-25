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

// Yönetim panelinin GÖZLEM uçları: durum özeti, güvenlik olayları, başarısız
// yönetim işlemleri, tıklama raporu.
// AdminEndpoints.cs'ten panel sekmelerine göre ayrıldı (güvenlik/mimari incelemesi,
// 26 Eylül); uçlar birebir aynı, hepsi X-Admin-Key ile korunuyor.
internal static class AdminGozlemEndpoints
{
    public static void MapAdminGozlemEndpoints(this WebApplication app, string? adminApiKey)
    {
        // Guvenlik olaylari - panelin "akis" gorunumunun kaynagi.
        //
        // OZET LISTEDEN AYRI SORGULANIYOR: liste `take` ile kirpiliyor ve
        // kirpilmis listeden sayi cikarmak yaniltir ("3 saldiri var" derken
        // aslinda 3.000 olabilir).
        // Yonetim panelinin "Durum" ekrani - TEK istekte ozet.
        //
        // NEDEN AYRI BIR UC: panel /yonetim/api/* uzerinden geliyor ve Caddy o
        // yolu /api/dev/* olarak yeniden yaziyor, yani panel /api/stats ya da
        // /api/health/sources gibi /api/dev disindaki uclara ULASAMIYOR. Caddy'ye
        // ikinci bir kural eklemek yerine tek bir ozet ucu yazildi: panel tek
        // istek atiyor ve hangi verinin panele acildigi TEK YERDE gorunuyor.
        //
        // KAYNAK TAZELIGI BURADA YENIDEN YORUMLANMIYOR. Bayat/emekli esikleri
        // /api/health/sources'ta tanimli ve alarm oradan (UptimeRobot) geliyor;
        // ayni kurali ikinci kez yazmak, iki kopyanin zamanla ayrismasi demekti.
        // Burada yalnizca HAM son tarama zamanlari donuyor, yorumu arayuz yapiyor.
        app.MapGet("/api/dev/durum", async (AppDbContext db, CancellationToken ct) =>
        {
            // Panel gercegi gostermeli: gizlenmis urunler de katalogun parcasi.
            var urunler = await db.Products
                .IgnoreQueryFilters()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    toplam = g.Count(),
                    besinli = g.Count(p => p.NutritionJson != null),
                    tiklama = g.Sum(p => p.ClickCount),
                    sonBesinTuru = g.Max(p => p.NutritionCheckedAt),
                })
                .FirstOrDefaultAsync(ct);

            var markaSayisi = await db.Brands.CountAsync(ct);

            var aboneler = await db.Subscribers
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    onayli = g.Count(x => x.IsConfirmed && x.UnsubscribedAt == null),
                    bekleyen = g.Count(x => !x.IsConfirmed && x.UnsubscribedAt == null),
                })
                .FirstOrDefaultAsync(ct);

            // Kaynak birimi COALESCE(Seller, Brand.Name) - saglik ucuyla ayni
            // tanim. En eskiden baslayarak ilk 12 kaynak yeterli; panel bir
            // izleme araci degil, hizli bakis.
            var kaynaklar = await db.Products
                .IgnoreQueryFilters()
                .Where(p => p.LatestScrapedAt != null)
                .GroupBy(p => p.Seller ?? p.Brand!.Name)
                .Select(g => new { kaynak = g.Key, sonTarama = g.Max(p => p.LatestScrapedAt) })
                .OrderBy(x => x.sonTarama)
                .Take(12)
                .ToListAsync(ct);

            var gunOnce = DateTimeOffset.UtcNow.AddDays(-1);
            var olayOzeti = await db.SecurityEvents
                .Where(x => x.OccurredAt >= gunOnce)
                .GroupBy(x => x.Kind)
                .Select(g => new { kind = g.Key, count = g.Count() })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                urun = new
                {
                    toplam = urunler?.toplam ?? 0,
                    besinli = urunler?.besinli ?? 0,
                    markaSayisi,
                },
                tiklamaToplam = urunler?.tiklama ?? 0,
                besin = new
                {
                    sonTur = urunler?.sonBesinTuru,
                    siradakiTur = urunler?.sonBesinTuru?.AddDays(2),
                },
                abone = new
                {
                    onayli = aboneler?.onayli ?? 0,
                    bekleyen = aboneler?.bekleyen ?? 0,
                },
                kaynaklar,
                sonGunOlaylari = olayOzeti,
            });
        }).RequireAdminKey(adminApiKey);

        // Yonetim islemlerinin BASARISIZLIK sebepleri. Panelin "Olaylar"
        // sekmesinde guvenlik olaylarinin YANINDA ama AYRI gosteriliyor:
        // ikisi farkli sorulara cevap veriyor (biri "bana kim saldiriyor",
        // digeri "benim islemim neden olmadi") ve tek listede birlestirmek
        // suc duyurusuna dayanak olan listeyi kendi hatalarimizla kirletirdi.
        app.MapGet("/api/dev/admin-failures", async (
            AppDbContext db, int? limit, int? days, CancellationToken ct) =>
        {
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days ?? 7, 1, 365));
            var take = Math.Clamp(limit ?? 100, 1, 500);

            var kayitlar = await db.AdminOperationFailures.AsNoTracking()
                .Where(x => x.OccurredAt >= since)
                .OrderByDescending(x => x.OccurredAt)
                .Take(take)
                .ToListAsync(ct);

            return Results.Ok(kayitlar);
        }).RequireAdminKey(adminApiKey);

        app.MapGet("/api/dev/security-events", async (
            AppDbContext db, string? kind, string? ip, int? limit, int? days, CancellationToken ct) =>
        {
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days ?? 7, 1, 365));
            var take = Math.Clamp(limit ?? 200, 1, 1000);

            var query = db.SecurityEvents.AsNoTracking().Where(x => x.OccurredAt >= since);
            if (!string.IsNullOrWhiteSpace(kind))
                query = query.Where(x => x.Kind == kind);
            if (!string.IsNullOrWhiteSpace(ip))
                query = query.Where(x => x.Ip == ip);

            var events = await query
                .OrderByDescending(x => x.OccurredAt)
                .Take(take)
                .ToListAsync(ct);

            var summary = await query
                .GroupBy(x => x.Kind)
                .Select(g => new { kind = g.Key, count = g.Count() })
                .ToListAsync(ct);

            // Bir suc duyurusunda ilk sorulan sey "hangi adres, kac kez, ne zaman".
            var topIps = await query
                .GroupBy(x => x.Ip)
                .Select(g => new
                {
                    ip = g.Key,
                    count = g.Count(),
                    firstSeen = g.Min(x => x.OccurredAt),
                    lastSeen = g.Max(x => x.OccurredAt),
                })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToListAsync(ct);

            return Results.Ok(new { events, summary, topIps });
        }).RequireAdminKey(adminApiKey);

        // Markalara "bu hafta size şu kadar tıklama gönderdik" raporu hazırlamak
        // için — ClickCount tarihsiz/kümülatif bir sayaç olduğundan (tek tek
        // tıklama zaman damgası tutulmuyor) burada dönen sayılar site açılışından
        // beri toplam tıklamalar. Haftalık rapor için: bu endpoint'i her hafta
        // aynı gün çalıştırıp bir önceki haftanın sayısından fark alınmalı
        // (elle, tarih bazlı bir tıklama günlüğü tutmak MVP'de aşırı mühendislik).
        app.MapGet("/api/dev/click-report", async (AppDbContext db, CancellationToken ct) =>
        {
            var report = await db.Products
                .Where(p => p.Brand!.IsActive)
                .GroupBy(p => p.Brand!.Name)
                .Select(g => new
                {
                    Brand = g.Key,
                    TotalClicks = g.Sum(p => p.ClickCount),
                    ProductCount = g.Count(),
                    TopProducts = g.OrderByDescending(p => p.ClickCount).Take(5).Select(p => new { p.Name, p.ClickCount }),
                })
                .OrderByDescending(r => r.TotalClicks)
                .ToListAsync(ct);

            return Results.Ok(report);
        }).RequireAdminKey(adminApiKey);
    }
}

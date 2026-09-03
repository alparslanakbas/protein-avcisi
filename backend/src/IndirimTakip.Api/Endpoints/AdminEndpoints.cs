using IndirimTakip.Core.Caching;
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

// Yönetim uçları (/api/dev/*). HEPSİ X-Admin-Key ile korunuyor —
// 2026-08-15'te bu uçlar korumasızdı ve herkes tarama tetikleyip sahte
// kupon ekleyebiliyordu.
internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app, string? adminApiKey)
    {
        // Taramayı elle tetiklemek için. İş ARKA PLANDA çalışıyor, uç hemen 202
        // dönüyor.
        //
        // NEDEN: eskiden tarama isteğin İÇİNDE çalışıp bitince yanıt dönüyordu ve bu
        // uzun süren kaynaklarda hiç işe yaramıyordu. Cloudflare origin yanıtını
        // ~100-125 saniye bekleyip 524 dönüyor; bağlantı kesilince ASP.NET isteği
        // iptal ediyor, CancellationToken tetikleniyor ve HİÇBİR ŞEY KAYDEDİLMİYOR.
        // 1 Eylül'de Provitamin denemesinde tam olarak bu oldu: ~500 istek karşı
        // siteye gitti, veritabanına tek ürün yazılmadı. protein7 (~15 dk) ve
        // Provitamin (~38 dk) bu yolla hiç tetiklenemezdi.
        //
        // İki incelik:
        //   • İstek kapsamı yanıt döner dönmez atılıyor, bu yüzden arka plan işi
        //     KENDİ kapsamını açıp scraper'ı oradan çözüyor.
        //   • İptal jetonu isteğe değil UYGULAMA ÖMRÜNE bağlı; yoksa aynı hatayı
        //     başka bir kılıkta tekrarlardık.
        //
        // Aynı kaynağın eşzamanlı taranmasına karşı koruma ScrapeIngestionService'te
        // zaten var (ikinci tetikleme reddediliyor), burada tekrarlanmıyor.
        app.MapPost("/api/dev/ingest/{brand}", (
            string brand,
            IEnumerable<IBrandScraper> scrapers,
            IServiceScopeFactory scopeFactory,
            IHostApplicationLifetime lifetime,
            ILoggerFactory loggerFactory) =>
        {
            var scraper = scrapers.FirstOrDefault(s => s.BrandName.Equals(brand, StringComparison.OrdinalIgnoreCase));
            if (scraper is null)
                return Results.NotFound($"'{brand}' için scraper bulunamadı.");

            var brandName = scraper.BrandName;
            var logger = loggerFactory.CreateLogger("ElleTarama");

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<ScrapeIngestionService>();
                var scoped = scope.ServiceProvider.GetServices<IBrandScraper>()
                    .First(s => s.BrandName == brandName);

                try
                {
                    logger.LogInformation("Elle tetiklenen tarama başladı: {Brand}.", brandName);
                    var count = await ingestion.IngestAsync(scoped, lifetime.ApplicationStopping);

                    // Veri değişti: önbelleği düşür ve sıcak uçları yeniden doldur.
                    // Elle tarama çoğunlukla deploy sonrası çalıştırılıyor, yani tam
                    // da ziyaretçinin soğuk önbelleğe düşeceği an.
                    // Fiyat özeti ÖNCE: önbellek ısıtması bu alanları okuyor,
                    // ters sırada ısıtma eski özeti önbelleğe alırdı.
                    await scope.ServiceProvider.GetRequiredService<PriceSummaryRefresher>()
                        .RefreshAsync(lifetime.ApplicationStopping);

                    await scope.ServiceProvider.GetRequiredService<IPublicCacheRefresher>()
                        .RefreshAsync(lifetime.ApplicationStopping);

                    logger.LogInformation("Elle tetiklenen tarama bitti: {Brand}, {Count} ürün.", brandName, count);
                }
                catch (Exception ex)
                {
                    // Yutulmamalı: arka plan işinin sessizce ölmesi, tam da bu ucun
                    // çözmeye çalıştığı "çalışıyor sandım ama veri yok" durumudur.
                    logger.LogError(ex, "Elle tetiklenen tarama BAŞARISIZ: {Brand}.", brandName);
                }
            });

            // Sonuç loglardan ve veritabanından izlenir; istemcinin bağlantıyı açık
            // tutmasına gerek yok.
            return Results.Accepted(value: new
            {
                brand = brandName,
                durum = "tarama arka planda başlatıldı",
                nasilIzlenir = "docker compose logs backend | grep 'Elle tetiklenen tarama'",
            });
        }).RequireAdminKey(adminApiKey);

        // Geçici elle-ekleme endpoint'i (roadmap'teki /api/dev/ingest ile aynı desende):
        // kupon kodları scrape edilmiyor, elle doğrulanıp buradan ekleniyor. Henüz auth
        // yok — /api/dev/ingest gibi bu da site canlıya çıkmadan önce korumaya alınmalı.
        app.MapPost("/api/dev/coupons", async (CreateCouponRequest request, CouponService coupons, CancellationToken ct) =>
        {
            if (!request.HasExactlyOneTarget)
                return Results.BadRequest("Kupon yalnızca bir markaya veya bir satıcıya bağlanmalıdır.");
            // Kod BİLİNÇLİ olarak zorunlu değil: her kampanyanın girilecek bir kodu
            // yok (ör. üyelikle otomatik uygulanan "ilk alışverişte ek %5"). Açıklama
            // ise zorunlu — kullanıcının kutuda göreceği tek metin o.
            if (string.IsNullOrWhiteSpace(request.Description))
                return Results.BadRequest("Kupon açıklaması boş olamaz.");

            var result = await coupons.CreateAsync(request, ct);
            return result is null ? Results.NotFound($"'{request.BrandName}' adında marka bulunamadı.") : Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // Süresi geçen/yanlış çıkan bir kuponu deaktive edebilmek için (Article'daki
        // PUT deseniyle aynı) — önceden sadece ekleme vardı, bir kuponu kapatmanın
        // API üzerinden hiçbir yolu yoktu.
        app.MapPut("/api/dev/coupons/{id:int}", async (int id, UpdateCouponRequest request, CouponService coupons, CancellationToken ct) =>
        {
            var result = await coupons.UpdateAsync(id, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // Kapsam dışı kalan ürünleri (ör. bir markanın feed'inde karışan giyim/
        // ekipman ürünleri — bkz. HiqScraper'daki "type:wearable"/"type:equipment"
        // filtresi) elle temizlemek için. Cascade delete sayesinde ilişkili
        // PriceHistory/ProductFavorite/ProductWatch kayıtları da otomatik siliniyor.
        // Scraper filtresi zaten kurulduğu için silinen ürün bir sonraki taramada
        // geri gelmiyor.
        app.MapDelete("/api/dev/products/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null) return Results.NotFound();
            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).RequireAdminKey(adminApiKey);

        app.MapPost("/api/dev/articles", async (CreateArticleRequest request, ArticleService articles, CancellationToken ct) =>
        {
            var result = await articles.CreateAsync(request, ct);
            return result is null ? Results.Conflict($"'{request.Slug}' slug'ı zaten kullanılıyor.") : Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // Mevcut bir yazıyı düzenlemek için (ör. derinleştirme) — kısmi güncelleme,
        // gönderilmeyen alanlar olduğu gibi kalır.
        app.MapPut("/api/dev/articles/{slug}", async (string slug, UpdateArticleRequest request, ArticleService articles, CancellationToken ct) =>
        {
            var result = await articles.UpdateAsync(slug, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // Asıl gönderim artık DigestBackgroundService ile haftada bir otomatik
        // tetikleniyor — bu endpoint elle/anlık test tetiklemesi için hâlâ duruyor
        // (aynı /api/dev/ingest deseninde, BackgroundService eklendikten sonra da).
        app.MapPost("/api/dev/send-digest", async (DigestService digest, HttpContext http, CancellationToken ct) =>
        {
            var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            var result = await digest.SendDigestAsync(baseUrl, ct);
            return Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // Asıl tamamlama artık DescriptionBackfillBackgroundService ile haftada bir
        // otomatik tetikleniyor — bu endpoint elle/anlık test tetiklemesi için
        // (aynı /api/dev/* desende).
        app.MapPost("/api/dev/backfill-descriptions", async (ProductDetailBackfillService backfill, CancellationToken ct) =>
        {
            var updated = await backfill.BackfillAsync(ct);
            return Results.Ok(new { updatedCount = updated });
        }).RequireAdminKey(adminApiKey);

        // Markaların sitelerindeki yıldız ortalamasını elle tazelemek için. Asıl
        // mekanizma RatingRefreshBackgroundService (6 saatte bir, en eski kontrol
        // edilenlerden başlayarak); bu uç ilk doldurma ve anlık kontrol için.
        app.MapPost("/api/dev/refresh-ratings", async (ProductRatingRefreshService ratings, int? max, CancellationToken ct) =>
        {
            var updated = await ratings.RefreshAsync(max, ct);
            return Results.Ok(new { updatedCount = updated });
        }).RequireAdminKey(adminApiKey);

        // Porsiyon (servis) büyüklüğü çıkarımı, açıklamalar DB'ye yazıldıktan SONRA
        // eklendi — bu endpoint, zaten kayıtlı açıklamaları yeniden okuyup eksik
        // ServingSizeGrams'ları tek seferde dolduruyor. Markalara hiç istek atmıyor
        // (tamamen DB içi bir işlem), bu yüzden yeniden tarama gerekmiyor. Sonraki
        // taramalarda/backfill'lerde aynı çıkarım otomatik yapılıyor, bu endpoint
        // yalnızca geçmişi tamamlamak için.
        app.MapPost("/api/dev/backfill-serving-sizes", async (AppDbContext db, CancellationToken ct) =>
        {
            var candidates = await db.Products
                .Where(p => p.ServingSizeGrams == null && p.Description != null)
                .ToListAsync(ct);

            var updated = 0;
            foreach (var product in candidates)
            {
                var grams = ProductAttributeParser.ExtractServingSizeGrams(product.Description);
                if (grams is null)
                    continue;

                product.ServingSizeGrams = grams;
                updated++;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { candidateCount = candidates.Count, updatedCount = updated });
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

        // Site haritasındaki TÜM adresleri arama motorlarına bildirir (IndexNow).
        // Normal akışta yalnızca yeni ürünler bildiriliyor; bu uç ilk kurulum ve
        // toplu yeniden bildirim için. Bing sitemap'i almasına rağmen siteyi hiç
        // dizinlemediği için (2026-08-28 ölçümü) ilk toplu bildirim gerekiyordu.
        app.MapPost("/api/dev/indexnow/submit-all", async (
            DealsQueryService deals, IndexNowClient indexNow, IConfiguration config, CancellationToken ct) =>
        {
            if (!indexNow.IsEnabled)
                return Results.BadRequest(new { message = "IndexNow devre dışı ya da anahtar tanımlı değil." });

            var frontendBaseUrl = (config["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr").TrimEnd('/');
            var entries = await deals.GetSitemapEntriesAsync(ct);

            var urls = new List<string> { frontendBaseUrl };
            urls.AddRange(entries.Select(e => $"{frontendBaseUrl}/urun/{e.Id}/{Slugifier.Slugify(e.Name)}"));

            var sent = await indexNow.SubmitAsync(urls, ct);
            return Results.Ok(new { submitted = sent, total = urls.Count });
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

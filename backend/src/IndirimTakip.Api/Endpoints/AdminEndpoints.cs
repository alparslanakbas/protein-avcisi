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

// Yönetim uçları (/api/dev/*). HEPSİ X-Admin-Key ile korunuyor —
// 2026-08-15'te bu uçlar korumasızdı ve herkes tarama tetikleyip sahte
// kupon ekleyebiliyordu.
internal static class AdminEndpoints
{
    // Bilinmeyen ürün 404; reddedilen değer sebebiyle 400 (panel olduğu gibi
    // gösteriyor); aksi hâlde liste önbelleği tazeleniyor, yoksa değişiklik çıktı
    // önbelleğinin bir saati boyunca sitede görünmezdi.
    private static async Task<IResult> ElleDuzenlemeYaniti(ElleDuzenlemeSonucu sonuc, int id, IPublicCacheRefresher cache, CancellationToken ct)
    {
        if (!sonuc.Bulundu)
            return Results.NotFound($"{id} numaralı ürün bulunamadı.");
        if (!sonuc.Kabul)
            return Results.BadRequest(new { message = sonuc.Sebep, kod = sonuc.Kod });

        await cache.RefreshAsync(ct);
        return Results.Ok(new { guncellenenSatir = sonuc.GuncellenenSatir });
    }

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
            return result is null ? Results.NotFound($"{id} numaralı kupon bulunamadı.") : Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

        // Kapsam dışı kalan ürünleri (ör. bir markanın feed'inde karışan giyim/
        // ekipman ürünleri — bkz. HiqScraper'daki "type:wearable"/"type:equipment"
        // filtresi) elle temizlemek için. Cascade delete sayesinde ilişkili
        // PriceHistory/ProductFavorite/ProductWatch kayıtları da otomatik siliniyor.
        // Scraper filtresi zaten kurulduğu için silinen ürün bir sonraki taramada
        // geri gelmiyor.
        app.MapDelete("/api/dev/products/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            // FindAsync global filtreden etkilenmiyor ama acikca belirtiyoruz:
            // gizlenmis bir urun de silinebilmeli.
            var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound($"{id} numaralı ürün bulunamadı.");
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
            return result is null ? Results.NotFound($"'{slug}' slug'ıyla yazı bulunamadı.") : Results.Ok(result);
        }).RequireAdminKey(adminApiKey);

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

        // --- Marka ve urun gorunurlugu (yonetim paneli) ---
        //
        // MARKA TARAFI ZATEN CALISIYORDU: Brand.IsActive alani bastan beri var
        // ve DealsQueryService 14 ayri sorguda kontrol ediyor. Eksik olan
        // yalnizca onu acip kapatacak bir arayuzdu.
        //
        // URUN TARAFI YENI: Product.IsActive + AppDbContext'te global sorgu
        // filtresi. Filtre tek yerde durdugu icin gizlenen urun butun
        // sayfalardan ve sitemap'ten kendiliginden dusuyor.
        app.MapGet("/api/dev/markalar", async (AppDbContext db, CancellationToken ct) =>
        {
            var markalar = await db.Brands
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.IsActive,
                    // Gizli urunler de sayiliyor: panelde "kac urunu var"
                    // sorusunun cevabi katalogun tamami olmali.
                    urunSayisi = db.Products.IgnoreQueryFilters().Count(p => p.BrandId == b.Id),
                    gizliUrun = db.Products.IgnoreQueryFilters().Count(p => p.BrandId == b.Id && !p.IsActive),
                })
                .OrderBy(b => b.Name)
                .ToListAsync(ct);

            return Results.Ok(markalar);
        }).RequireAdminKey(adminApiKey);

        app.MapPut("/api/dev/markalar/{id:int}", async (
            int id, GorunurlukIstegi istek, AppDbContext db, IPublicCacheRefresher cache, CancellationToken ct) =>
        {
            var marka = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (marka is null)
                return Results.NotFound($"{id} numaralı marka bulunamadı.");

            marka.IsActive = istek.IsActive;
            await db.SaveChangesAsync(ct);

            // Onbellek tazelenmezse degisiklik bir saat boyunca sitede
            // gorunmuyor ve "calismadi" sanilir (cikti onbellegi 1 saat).
            await cache.RefreshAsync(ct);
            return Results.Ok(new { marka.Id, marka.Name, marka.IsActive });
        }).RequireAdminKey(adminApiKey);

        // Katalogda 5.000+ urun var; listeleme ARAMAYA ya da bir filtreye bagli.
        // Filtreler elle veri girisinin is listeleri: besin degeri eksik, kategorisiz,
        // ve yalnizca bir kisinin doldurabilecegi urunler.
        //
        // SAYFALI, TOPLAMLI. Eskiden ilk 200 satiri donup duruyordu: bir filtre
        // binlerce satira uyunca 200'den sonrasina (markaya gore sirali) hic
        // ulasilamiyordu ve listenin kesildigini soyleyen bir sey yoktu. Son siralama
        // anahtari Id: ayni marka ve adli satirlar sayfalar arasinda tekrarlanmiyor ya
        // da kaybolmuyor.
        app.MapGet("/api/dev/urunler", async (
            AppDbContext db, IEnumerable<IBrandScraper> kaziyicilar, string? ara, bool? yalnizGizli,
            bool? eksikBesin, bool? kategorisiz, bool? elleGirilmeli, bool? elleGirilmis,
            int? sayfa, int? sayfaBoyutu, CancellationToken ct) =>
        {
            var sorgu = db.Products.IgnoreQueryFilters().AsNoTracking();

            if (yalnizGizli == true)
                sorgu = sorgu.Where(p => !p.IsActive);
            // "Tablosu yok" diye elle kapatilan urun bu IS LISTESINE girmiyor:
            // karari verilmis bir satiri her acilista yeniden gormek, listeyi
            // bakilmayan bir alarma cevirir.
            if (eksikBesin == true)
                sorgu = sorgu.Where(p => p.NutritionJson == null && !p.NutritionIsManual);
            if (kategorisiz == true)
                sorgu = sorgu.Where(p => p.Category == null);

            // ELLE GIRILEN: kisinin kendi isini geri bulmasi icin. Besin ve kategori
            // bayraklarinin ikisi de sayiliyor, cunku ikisi de o kisinin karari;
            // "tablosu yok" diye kapatilan urun de burada, tablosu bos olsa bile.
            if (elleGirilmis == true)
                sorgu = sorgu.Where(p => p.NutritionIsManual || p.CategoryIsManual);

            // OTOMATIK KAYNAK KALMAMIS: yalnizca bir kisinin doldurabilecegi satirlar.
            // Birkac saat icinde detay tamamlamanin dolduracagi bir urunu elle yazmak bosa
            // emek; o yuzden hala dolabilecek satir listeye girmiyor: detay cekicisi olan
            // bir markanin KENDI sitesindeki (Seller == null) henuz bakilmamis urunu. Normal
            // tarama her satir icin zaten calisti; doldurmadigini sonra da doldurmaz.
            if (elleGirilmeli == true)
            {
                var detayMarkalari = kaziyicilar.OfType<IProductDetailFetcher>()
                    .Select(k => ((IBrandScraper)k).BrandName)
                    .ToArray();
                sorgu = sorgu.Where(p => p.NutritionJson == null
                    && !(p.Seller == null && p.NutritionCheckedAt == null && detayMarkalari.Contains(p.Brand!.Name)));
            }

            if (!string.IsNullOrWhiteSpace(ara))
            {
                // Turkce buyuk/kucuk harf tuzagi: ILIKE yerine iki tarafi da
                // ayni sekilde kucultmek gerekiyor. Postgres'in lower()'i
                // veritabani locale'ine gore calisiyor ve bu projede locale
                // C.UTF-8 (builtin) - yani ASCII disi harflerde katlama YOK.
                // Bu yuzden arama, kullanicinin yazdigi bicimle eslesecek
                // sekilde hem ham hem kucultulmus haliyle deneniyor.
                var ham = ara.Trim();
                var kucuk = ham.ToLowerInvariant();
                sorgu = sorgu.Where(p =>
                    EF.Functions.ILike(p.Name, "%" + ham + "%")
                    || EF.Functions.ILike(p.Name, "%" + kucuk + "%")
                    || EF.Functions.ILike(p.Brand!.Name, "%" + ham + "%"));
            }
            // YENI FILTRE EKLEYEN BURAYI DA GUNCELLEMELI: bu kosulda sayilmayan
            // bir filtre sessizce bos liste dondurur, sorgu hic calismaz.
            else if (yalnizGizli != true && eksikBesin != true && kategorisiz != true
                && elleGirilmeli != true && elleGirilmis != true)
            {
                // Arama da filtre de yoksa liste anlamsiz derecede buyuk olurdu.
                return Results.Ok(new { urunler = Array.Empty<object>(), toplam = 0, sayfa = 1, sayfaBoyutu = 0 });
            }

            var boyut = Math.Clamp(sayfaBoyutu ?? 50, 10, 100);
            var gecerliSayfa = Math.Max(1, sayfa ?? 1);
            var toplam = await sorgu.CountAsync(ct);

            var urunler = await sorgu
                .OrderBy(p => p.Brand!.Name)
                .ThenBy(p => p.Name)
                .ThenBy(p => p.Id)
                .Skip((gecerliSayfa - 1) * boyut)
                .Take(boyut)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    marka = p.Brand!.Name,
                    p.Seller,
                    p.IsActive,
                    p.LatestPrice,
                    p.Category,
                    p.CategoryIsManual,
                    p.NutritionJson,
                    p.NutritionIsManual,
                    p.ServingSizeGrams,
                    p.ServingsPerPackage,
                })
                .ToListAsync(ct);

            return Results.Ok(new { urunler, toplam, sayfa = gecerliSayfa, sayfaBoyutu = boyut });
        }).RequireAdminKey(adminApiKey);

        // --- Elle girilen kategori ve besin değeri (bkz. ManualProductDataService) ---
        app.MapPut("/api/dev/urunler/{id:int}/kategori", async (
            int id, KategoriIstegi istek, ManualProductDataService veri, IPublicCacheRefresher cache, CancellationToken ct) =>
            await ElleDuzenlemeYaniti(await veri.KategoriAyarlaAsync(id, istek.Kategori, ct), id, cache, ct))
            .RequireAdminKey(adminApiKey);

        app.MapPut("/api/dev/urunler/{id:int}/besin", async (
            int id, ElleBesinIstegi istek, ManualProductDataService veri, IPublicCacheRefresher cache, CancellationToken ct) =>
            await ElleDuzenlemeYaniti(await veri.BesinAyarlaAsync(id, istek, ct), id, cache, ct))
            .RequireAdminKey(adminApiKey);

        app.MapDelete("/api/dev/urunler/{id:int}/besin", async (
            int id, ManualProductDataService veri, IPublicCacheRefresher cache, CancellationToken ct) =>
            await ElleDuzenlemeYaniti(await veri.BesinTemizleAsync(id, ct), id, cache, ct))
            .RequireAdminKey(adminApiKey);

        // Silmekten ayri bir uc: bu "kaynakta tablo YOK" karari, "tabloyu at ve
        // yeniden bak" degil. Ikisi ayni ucta bayrakla birlesseydi, yanlis okunmus
        // bir tabloyu temizlemek isteyen biri urunu kazara otomatik kaynaklara
        // kapatabilirdi.
        app.MapPost("/api/dev/urunler/{id:int}/besin-yok", async (
            int id, ManualProductDataService veri, IPublicCacheRefresher cache, CancellationToken ct) =>
            await ElleDuzenlemeYaniti(await veri.BesinYokIsaretleAsync(id, ct), id, cache, ct))
            .RequireAdminKey(adminApiKey);

        app.MapPut("/api/dev/urunler/{id:int}", async (
            int id, GorunurlukIstegi istek, AppDbContext db, IPublicCacheRefresher cache, CancellationToken ct) =>
        {
            var urun = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (urun is null)
                return Results.NotFound($"{id} numaralı ürün bulunamadı.");

            urun.IsActive = istek.IsActive;
            await db.SaveChangesAsync(ct);

            // Ayni sebep: urun gizlenince liste onbellegi tazelenmeli.
            await cache.RefreshAsync(ct);
            return Results.Ok(new { urun.Id, urun.Name, urun.IsActive });
        }).RequireAdminKey(adminApiKey);

        // Kupon listesi - panelin duzenleme ekrani icin.
        // Genel /api/coupons ucu YALNIZCA aktif ve suresi gecmemis kuponlari
        // donduruyor (ziyaretcinin gormesi gereken bu). Panelde pasif ve
        // suresi gecmis olanlar da gorunmeli, yoksa bir kupon kapatildiginda
        // yonetim ekranindan da kaybolur ve geri acilamaz.
        app.MapGet("/api/dev/coupons", async (AppDbContext db, CancellationToken ct) =>
        {
            var kuponlar = await db.Coupons
                .AsNoTracking()
                .Include(c => c.Brand)
                .OrderByDescending(c => c.IsActive)
                .ThenByDescending(c => c.LastVerifiedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.Description,
                    c.BrandId,
                    brandName = c.Brand != null ? c.Brand.Name : null,
                    c.Seller,
                    c.ValidUntil,
                    c.LastVerifiedAt,
                    c.IsActive,
                })
                .ToListAsync(ct);

            return Results.Ok(kuponlar);
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

internal record GorunurlukIstegi(bool IsActive);

/// <summary>Kategori kodu; null ya da boş = otomatik kategoriye dön.</summary>
internal record KategoriIstegi(string? Kategori);

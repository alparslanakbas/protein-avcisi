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

// Katalog okuma uçları: indirim/ürün listeleri, marka ve kategori
// istatistikleri, sitemap. Hepsi genel veri önbelleğine tabi.
internal static class DealsEndpoints
{
    public static void MapDealsEndpoints(this WebApplication app, string cachePolicy)
    {
        // /api/deals, /api/products, /api/store-deals aynı sorgu parametrelerini
        // kabul edip sadece onlyDiscounted/onlyStoreDiscounted bayraklarıyla
        // ayrışıyordu — üç yerde neredeyse birebir kopya kod yerine tek bir yerden.
        void MapDealsQueryEndpoint(string route, bool onlyDiscounted, bool onlyStoreDiscounted)
        {
            app.MapGet(route, async (
                // sellers: ürünün satın alındığı yer (marka/üretici ile aynı şey değil).
                // "Markanın kendi sitesi" etiketi DealsQueryService'te NULL'a çevriliyor.
                DealsQueryService deals, string[]? brands, string[]? categories, string[]? sellers, string? search,
                decimal? minPrice, decimal? maxPrice, int? days, string? sortBy, int? page, int? pageSize,
                // Belirli bir bileşeni arayan sayfalar (ör. "Beta-Alanine Dozu"
                // hesaplayıcısı) eşanlamlı genişletmeyi KAPATABİLİR: "alanine"
                // araması, o kelime amino-asitler kategorisinin anahtar
                // kelimelerinden biri olduğu için kategorinin TAMAMINI döndürüyordu
                // (arginin ürünleri beta-alanine sayfasında listeleniyordu).
                bool? expandSynonyms,
                // Marka sayfası bunu true gönderiyor: markanın kendi mağazası varsa
                // yalnızca onu göster (bkz. DealsQueryService.GetDealsAsync).
                bool? preferBrandStore,
                CancellationToken ct) =>
            {
                var windowDays = days is null or <= 0 ? 30 : days.Value;
                var result = await deals.GetDealsAsync(
                    windowDays, brands, categories, sellers, search, minPrice, maxPrice,
                    onlyDiscounted, onlyStoreDiscounted, sortBy,
                    page is null or <= 0 ? 1 : page.Value, EndpointHelpers.NormalizePageSize(pageSize), ct,
                    expandSearchSynonyms: expandSynonyms ?? true,
                    preferBrandStore: preferBrandStore ?? false);
                return Results.Ok(result);
            }).CacheOutput(cachePolicy);
        }

        MapDealsQueryEndpoint("/api/deals", onlyDiscounted: true, onlyStoreDiscounted: false);
        MapDealsQueryEndpoint("/api/products", onlyDiscounted: false, onlyStoreDiscounted: false);
        // Markanın kendi beyan ettiği (doğrulanmamış) kampanya/indirim fiyatına sahip ürünler.
        MapDealsQueryEndpoint("/api/store-deals", onlyDiscounted: false, onlyStoreDiscounted: true);

        // Marka karşılaştırma sayfaları için — kategori bazında ortalama fiyat.
        // brand1/brand2 nullable — ASP.NET Core minimal API'de non-nullable bir string
        // query parametresi bile eksik gönderilince null olarak bind edilebiliyor
        // (route parametrelerinin aksine query parametreleri otomatik zorunlu değil);
        // kontrol olmadan GetBrandComparisonAsync içindeki .ToLower() çağrısı
        // NullReferenceException'a düşüp 500 dönüyordu — TestSprite'ın otomatik
        // testinde yakalandı (bkz. CLAUDE.md).
        app.MapGet("/api/brand-comparison", async (string? brand1, string? brand2, DealsQueryService deals, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(brand1) || string.IsNullOrWhiteSpace(brand2))
                return Results.BadRequest(new { message = "brand1 ve brand2 parametreleri gerekli." });

            var result = await deals.GetBrandComparisonAsync(brand1, brand2, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Protein ihtiyacı hesaplayıcısının "servis başı en uygun ürünler" tablosu —
        // hesap Size metnini ayrıştırmayı gerektirdiği için SQL'e çevrilemiyor,
        // serviste bellek içinde yapılıyor; buradan yalnızca ilk N ürün dönüyor
        // (sayfanın tüm kategoriyi çekmesi SSR çıktısını 451 KB'a çıkarıyordu).
        app.MapGet("/api/best-value-per-serving", async (
            string? category,
            string[]? brands,
            string? search,
            int? page,
            int? pageSize,
            DealsQueryService deals,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(category))
                return Results.BadRequest(new { message = "category parametresi gerekli." });

            var result = await deals.GetBestValuePerServingAsync(
                category,
                brands is { Length: > 0 } ? brands : null,
                string.IsNullOrWhiteSpace(search) ? null : search,
                page is null or <= 0 ? 1 : page.Value,
                EndpointHelpers.NormalizePageSize(pageSize),
                ct);

            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Marka × kategori kesişim sayfaları (/marka/:brand/:category) — sitemap ve
        // iç linkler yalnızca gerçekten ürünü olan çiftleri kullanıyor.
        app.MapGet("/api/brand-category-pairs", async (DealsQueryService deals, CancellationToken ct) =>
        {
            var result = await deals.GetBrandCategoryPairsAsync(ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Markalar dizini (/markalar) — marka başına ürün sayısı, tek istekte.
        // Dizin bu sayıyı brand-category-pairs'ı toplayarak hesaplıyordu ve
        // kategorisiz ürünleri kaçırıyordu; artık marka sayfasıyla aynı tanım.
        app.MapGet("/api/brand-product-counts", async (DealsQueryService deals, CancellationToken ct) =>
        {
            var result = await deals.GetBrandProductCountsAsync(ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Hesaplayıcı tablosundaki marka çipleri — yalnızca o kategoride servis
        // başı fiyatı hesaplanabilen ürünü olan markalar.
        app.MapGet("/api/best-value-brands", async (string? category, DealsQueryService deals, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(category))
                return Results.BadRequest(new { message = "category parametresi gerekli." });

            var result = await deals.GetBestValueBrandsAsync(category, ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        app.MapGet("/api/filters", async (DealsQueryService deals, CancellationToken ct) =>
        {
            var result = await deals.GetFilterOptionsAsync(ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Ana sayfadaki "canlı tarama şeridi" için özet sayılar.
        app.MapGet("/api/stats", async (DealsQueryService deals, CancellationToken ct) =>
        {
            var result = await deals.GetHomepageStatsAsync(cancellationToken: ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Ana sayfadaki "Kullanıcıların tercih ettikleri" bandı — sıralama gerçek
        // favori ve tıklama sayaçlarından geliyor (bkz. GetPreferredProductsAsync).
        app.MapGet("/api/preferred-products", async (DealsQueryService deals, int? count, CancellationToken ct) =>
        {
            // Band kategori sekmeleriyle daraltılabildiği için istemci geniş bir
            // havuz istiyor; üst sınır kötüye kullanıma karşı sabit.
            var take = Math.Clamp(count ?? 60, 1, 100);
            var result = await deals.GetPreferredProductsAsync(take, cancellationToken: ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Marka sayfasındaki "bu markaya genel bakış" bölümü için — kendi verimize
        // dayanan, kopyalanmamış özgün içerik (bkz. DealsQueryService.GetBrandStatsAsync).
        // category verilirse istatistikler markanın yalnızca o kategorideki
        // ürünlerinden hesaplanır (marka × kategori sayfaları için).
        app.MapGet("/api/brand-stats", async (string? brand, string? category, DealsQueryService deals, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(brand))
                return Results.BadRequest(new { message = "brand parametresi gerekli." });

            var result = await deals.GetBrandStatsAsync(
                brand, category: string.IsNullOrWhiteSpace(category) ? null : category, cancellationToken: ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Ürün incelemesi sayfasındaki "bu ürün kategorisinde nasıl konumlanıyor"
        // bölümü için — bkz. DealsQueryService.GetCategoryPriceStatsAsync.
        app.MapGet("/api/category-price-stats", async (string? category, DealsQueryService deals, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(category))
                return Results.BadRequest(new { message = "category parametresi gerekli." });

            var result = await deals.GetCategoryPriceStatsAsync(category, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).CacheOutput(cachePolicy);

        app.MapGet("/api/products/{id:int}", async (int id, DealsQueryService deals, CancellationToken ct) =>
        {
            var result = await deals.GetProductByIdAsync(id, cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // sitemap.xml üretimi için — asıl XML frontend'in SSR sunucusunda kuruluyor
        // (kendi domain'ini biliyor), burası sadece ham veriyi veriyor.
        app.MapGet("/api/products/sitemap", async (DealsQueryService deals, CancellationToken ct) =>
        {
            var result = await deals.GetSitemapEntriesAsync(ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);
    }
}

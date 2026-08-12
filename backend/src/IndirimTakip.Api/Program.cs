using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// İzinli origin'ler appsettings'ten okunuyor (Development'ta localhost:4200,
// production'da hosting platformunun ortam değişkeniyle gerçek frontend
// domain'i eklenecek) — kod değişikliği gerekmeden ortam başına ayarlanabilsin.
const string CorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Geçici /api/dev/* endpoint'leri (elle tarama tetikleme, kupon ekleme) canlıya
// çıktıktan sonra da korumasız kaldı — herkes bu uçlara istek atıp taramayı
// tetikleyebilir ya da sahte kupon ekleyebilirdi. Tam bir kullanıcı/auth sistemi
// kurmak bu iki endpoint için aşırı mühendislik; basit bir paylaşılan anahtar
// (header'da) yeterli. Anahtar ayarlanmamışsa (ör. yerelde unutulduysa) güvenli
// tarafta kalıp erişimi tamamen reddediyoruz.
var adminApiKey = app.Configuration["AdminApiKey"];

// Uygulama açılırken bekleyen migration'ları otomatik uygula — hosting
// platformunda elle migration komutu çalıştırmaya gerek kalmasın diye
// (tek geliştiricili, küçük ölçekli bir proje için makul bir kısayol).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IndirimTakip.Infrastructure.AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

// Geçici tetikleme endpoint'i: gerçek zamanlanmış worker (roadmap'teki
// BackgroundService) eklenene kadar taramayı elle tetiklemek için.
app.MapPost("/api/dev/ingest/{brand}", async (string brand, IEnumerable<IBrandScraper> scrapers, ScrapeIngestionService ingestion, CancellationToken ct) =>
{
    var scraper = scrapers.FirstOrDefault(s => s.BrandName.Equals(brand, StringComparison.OrdinalIgnoreCase));
    if (scraper is null)
        return Results.NotFound($"'{brand}' için scraper bulunamadı.");

    var count = await ingestion.IngestAsync(scraper, ct);
    return Results.Ok(new { brand = scraper.BrandName, scrapedCount = count });
}).RequireAdminKey(adminApiKey);

app.MapGet("/api/deals", async (
    DealsQueryService deals, string[]? brands, string[]? categories, string? search,
    decimal? minPrice, decimal? maxPrice, int? days, string? sortBy, int? page, int? pageSize, CancellationToken ct) =>
{
    var windowDays = days is null or <= 0 ? 30 : days.Value;
    var result = await deals.GetDealsAsync(
        windowDays, brands, categories, search, minPrice, maxPrice,
        onlyDiscounted: true, onlyStoreDiscounted: false, sortBy,
        page is null or <= 0 ? 1 : page.Value, pageSize is null or <= 0 ? 24 : pageSize.Value, ct);
    return Results.Ok(result);
});

app.MapGet("/api/products", async (
    DealsQueryService deals, string[]? brands, string[]? categories, string? search,
    decimal? minPrice, decimal? maxPrice, int? days, string? sortBy, int? page, int? pageSize, CancellationToken ct) =>
{
    var windowDays = days is null or <= 0 ? 30 : days.Value;
    var result = await deals.GetDealsAsync(
        windowDays, brands, categories, search, minPrice, maxPrice,
        onlyDiscounted: false, onlyStoreDiscounted: false, sortBy,
        page is null or <= 0 ? 1 : page.Value, pageSize is null or <= 0 ? 24 : pageSize.Value, ct);
    return Results.Ok(result);
});

// Markanın kendi beyan ettiği (doğrulanmamış) kampanya/indirim fiyatına sahip ürünler.
app.MapGet("/api/store-deals", async (
    DealsQueryService deals, string[]? brands, string[]? categories, string? search,
    decimal? minPrice, decimal? maxPrice, int? days, string? sortBy, int? page, int? pageSize, CancellationToken ct) =>
{
    var windowDays = days is null or <= 0 ? 30 : days.Value;
    var result = await deals.GetDealsAsync(
        windowDays, brands, categories, search, minPrice, maxPrice,
        onlyDiscounted: false, onlyStoreDiscounted: true, sortBy,
        page is null or <= 0 ? 1 : page.Value, pageSize is null or <= 0 ? 24 : pageSize.Value, ct);
    return Results.Ok(result);
});

app.MapGet("/api/filters", async (DealsQueryService deals, CancellationToken ct) =>
{
    var result = await deals.GetFilterOptionsAsync(ct);
    return Results.Ok(result);
});

app.MapGet("/api/products/{id:int}", async (int id, DealsQueryService deals, CancellationToken ct) =>
{
    var result = await deals.GetProductByIdAsync(id, cancellationToken: ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// sitemap.xml üretimi için — asıl XML frontend'in SSR sunucusunda kuruluyor
// (kendi domain'ini biliyor), burası sadece ham veriyi veriyor.
app.MapGet("/api/products/sitemap", async (DealsQueryService deals, CancellationToken ct) =>
{
    var result = await deals.GetSitemapEntriesAsync(ct);
    return Results.Ok(result);
});

app.MapGet("/api/coupons", async (CouponService coupons, CancellationToken ct) =>
{
    var result = await coupons.GetActiveCouponsAsync(ct);
    return Results.Ok(result);
});

// Geçici elle-ekleme endpoint'i (roadmap'teki /api/dev/ingest ile aynı desende):
// kupon kodları scrape edilmiyor, elle doğrulanıp buradan ekleniyor. Henüz auth
// yok — /api/dev/ingest gibi bu da site canlıya çıkmadan önce korumaya alınmalı.
app.MapPost("/api/dev/coupons", async (CreateCouponRequest request, CouponService coupons, CancellationToken ct) =>
{
    var result = await coupons.CreateAsync(request, ct);
    return result is null ? Results.NotFound($"'{request.BrandName}' adında marka bulunamadı.") : Results.Ok(result);
}).RequireAdminKey(adminApiKey);

app.MapGet("/api/products/{id:int}/price-history", async (int id, int? days, PriceHistoryQueryService service, CancellationToken ct) =>
{
    var windowDays = days is null or <= 0 ? 30 : days.Value;
    var result = await service.GetPriceHistoryAsync(id, windowDays, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// Affiliate altyapısı: ürün linkleri buradan geçiyor ki ileride affiliate
// id eklemek kolay olsun (roadmap adım 7). Şimdilik sadece tıklama sayısını
// tutuyor, dış siteye 302 ile yönlendiriyor.
app.MapGet("/go/{productId:int}", async (int productId, AppDbContext db, CancellationToken ct) =>
{
    var product = await db.Products.FindAsync([productId], ct);
    if (product is null)
        return Results.NotFound();

    product.ClickCount++;
    await db.SaveChangesAsync(ct);

    return Results.Redirect(product.Url, permanent: false);
});

app.Run();

static class AdminAuthExtensions
{
    public static RouteHandlerBuilder RequireAdminKey(this RouteHandlerBuilder builder, string? expectedKey)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var providedKey = context.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
                return Results.Unauthorized();

            return await next(context);
        });
    }
}

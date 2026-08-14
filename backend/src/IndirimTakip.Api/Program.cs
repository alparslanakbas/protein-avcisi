using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
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

// "Rehber" bilgi yazıları — SEO/güven amaçlı, kupon deseniyle aynı: elle
// yazılıp elle eklenen içerik, otomatik üretilmiyor.
app.MapGet("/api/articles", async (ArticleService articles, CancellationToken ct) =>
{
    var result = await articles.GetPublishedArticlesAsync(ct);
    return Results.Ok(result);
});

app.MapGet("/api/articles/{slug}", async (string slug, ArticleService articles, CancellationToken ct) =>
{
    var result = await articles.GetBySlugAsync(slug, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/dev/articles", async (CreateArticleRequest request, ArticleService articles, CancellationToken ct) =>
{
    var result = await articles.CreateAsync(request, ct);
    return result is null ? Results.Conflict($"'{request.Slug}' slug'ı zaten kullanılıyor.") : Results.Ok(result);
}).RequireAdminKey(adminApiKey);

app.MapGet("/api/products/{id:int}/price-history", async (int id, int? days, PriceHistoryQueryService service, CancellationToken ct) =>
{
    var windowDays = days is null or <= 0 ? 30 : days.Value;
    var result = await service.GetPriceHistoryAsync(id, windowDays, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// "Haber Ver" — bir sonraki taramada bu ürünün fiyatı gerçekten düşerse
// tek seferlik bir bildirim e-postası gönderiliyor (bkz. ProductWatchNotifier).
app.MapPost("/api/products/{id:int}/watch", async (int id, WatchProductRequest request, ProductWatchService watchService, HttpContext http, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

    var confirmBaseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    var success = await watchService.WatchAsync(id, request, confirmBaseUrl, ct);
    return success ? Results.Ok(new { message = "Fiyat düşünce sana haber vereceğiz." }) : Results.NotFound();
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
});

// E-posta bülteni: double opt-in zorunlu (İYS/KVKK gereği) — bu endpoint
// hiçbir aboneyi doğrudan aktifleştirmiyor, sadece onay maili tetikliyor.
app.MapPost("/api/subscribe", async (SubscribeRequest request, SubscriberService subscribers, HttpContext http, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

    var confirmBaseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    await subscribers.SubscribeAsync(request, confirmBaseUrl, ct);
    return Results.Ok(new { message = "E-postanı kontrol et, onay bağlantısı gönderdik." });
});

// Onay/abonelikten çıkma linkleri e-postadan doğrudan tıklanıyor, bu yüzden
// JSON değil basit bir HTML sayfası dönüyor — ayrı bir frontend route'u
// kurmak bu iki statik mesaj için gereksiz olurdu. charset=utf-8 elle
// belirtilmezse tarayıcı Türkçe karakterleri bozuk gösterebiliyor.
app.MapGet("/api/subscribe/confirm/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
{
    var success = await subscribers.ConfirmAsync(token, ct);
    var html = success
        ? BuildInfoPage("Aboneliğin onaylandı!", "Artık öne çıkan indirimlerden haberdar olacaksın.")
        : BuildInfoPage("Bu bağlantı geçersiz.", "Onay linki süresi geçmiş ya da daha önce kullanılmış olabilir.");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/subscribe/unsubscribe/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
{
    var success = await subscribers.UnsubscribeAsync(token, ct);
    var html = success
        ? BuildInfoPage("Bültenden çıkarıldın.", "Fikrini değiştirirsen tekrar abone olabilirsin.")
        : BuildInfoPage("Bu bağlantı geçersiz.", "Bağlantı süresi geçmiş ya da daha önce kullanılmış olabilir.");
    return Results.Content(html, "text/html; charset=utf-8");
});

// Asıl gönderim artık DigestBackgroundService ile haftada bir otomatik
// tetikleniyor — bu endpoint elle/anlık test tetiklemesi için hâlâ duruyor
// (aynı /api/dev/ingest deseninde, BackgroundService eklendikten sonra da).
app.MapPost("/api/dev/send-digest", async (DigestService digest, HttpContext http, CancellationToken ct) =>
{
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    var result = await digest.SendDigestAsync(baseUrl, ct);
    return Results.Ok(result);
}).RequireAdminKey(adminApiKey);

app.Run();

// Onay/çıkış linklerinin ikisi de aynı markalı kart tasarımını kullanıyor —
// site genelindeki renk paletiyle (brand-600 yeşil, stone nötrleri) tutarlı.
static string BuildInfoPage(string heading, string message) => $"""
    <!doctype html>
    <html lang="tr">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>Protein Avcısı</title>
    </head>
    <body style="margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px;box-sizing:border-box;background:#fafaf9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
      <div style="max-width:420px;width:100%;background:#ffffff;border-radius:16px;box-shadow:0 4px 24px rgba(0,0,0,0.08);padding:40px 32px;text-align:center;">
        <div style="display:inline-flex;align-items:center;gap:8px;margin-bottom:28px;">
          <div style="width:36px;height:36px;border-radius:8px;background:#059669;color:#fff;font-weight:800;font-size:14px;display:flex;align-items:center;justify-content:center;">PA</div>
          <span style="font-size:18px;font-weight:700;color:#1c1917;">Protein<span style="color:#059669;">Avcısı</span></span>
        </div>
        <h1 style="font-size:20px;font-weight:800;color:#1c1917;margin:0 0 8px;">{heading}</h1>
        <p style="font-size:14px;color:#78716c;margin:0 0 28px;line-height:1.5;">{message}</p>
        <a href="https://proteinavcisi.com.tr" style="display:inline-block;background:#059669;color:#fff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 28px;border-radius:9999px;">Siteye Dön</a>
      </div>
    </body>
    </html>
    """;

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

record VoteRequest(bool Helpful);

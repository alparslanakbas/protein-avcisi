using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// Render/Cloudflare arkasında çalışıyoruz — gerçek istemci IP'si X-Forwarded-For
// header'ında geliyor, KnownProxies/KnownNetworks boş bırakılmazsa ASP.NET Core
// varsayılan olarak sadece loopback proxy'e güvenip header'ı yok sayar (rate
// limiter aşağıda IP'ye göre partition'lıyor, bu yüzden gerçek IP şart).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 2026-08-15: /api/subscribe ve /api/products/{id}/watch her çağrıda gerçek bir
// e-posta gönderiyor, hiçbir rate limit yoktu — bir bot/kötü niyetli istek aynı
// e-postayı art arda tek dakikada 20+ kez göndertip Brevo'nun o adresi kara
// listeye almasına yol açtı. IP başına 5 dakikada 5 istekle sınırlandı (SubscriberService'teki
// e-posta bazlı cooldown'a ek bir katman).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("EmailSensitive", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: RequestLoggingExtensions.GetClientIp(httpContext),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));

    // /go, /vote, favori silme gibi e-posta göndermeyen ama otomatik istekle
    // sayaç şişirmeye (ClickCount, HelpfulYesCount vb.) açık uçlar için daha
    // gevşek, genel bir limit — gerçek bir kullanıcının dakikada 60'tan fazla
    // ürün tıklaması/oylaması olağan değil, ama sayfada gezinirken rahatsız
    // etmeyecek kadar cömert.
    options.AddPolicy("General", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: RequestLoggingExtensions.GetClientIp(httpContext),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

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

// Onay/abonelikten çıkma sayfalarındaki "Siteye Dön" linki ve bültendeki
// ürün/site linkleri için — tek yerden yönetiliyor ki domain değişince
// (bu proje bu oturumda bile 2 kez değiştirdi) unutulan bir yer kalmasın.
var frontendBaseUrl = app.Configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr";

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

// UseForwardedHeaders MUTLAKA UseHsts'ten önce çalışmalı — HSTS middleware'i
// isteğin https olup olmadığına (Request.IsHttps) bakıp header'ı ona göre
// ekliyor/atlıyor; Render/Cloudflare TLS'i kendi ucunda sonlandırıp bize http
// ilettiği için X-Forwarded-Proto işlenmeden önce IsHttps hep false görünür
// ve HSTS header'ı sessizce hiç eklenmez (gerçek bir bug olarak yakalandı,
// bkz. CLAUDE.md 2026-08-15).
app.UseForwardedHeaders();

// Global hata yakalama — önceden yoktu, herhangi bir endpoint'te beklenmeyen
// bir exception çıplak, tutarsız bir 500 olarak dönüyordu (hiç loglanmadan).
// Exception detayını istemciye sızdırmıyoruz, sadece loglayıp genel bir JSON
// hata mesajı dönüyoruz.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is { } ex)
            app.Logger.LogError(ex, "İşlenmeyen hata: {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { message = "Beklenmeyen bir hata oluştu." });
    });
});

if (!app.Environment.IsDevelopment())
{
    // HSTS yereldeki http geliştirmeyi bozmasın diye sadece production'da.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();

// 2026-08-15 güvenlik denetimi: tarayıcı seviyesinde ek bir savunma katmanı
// hiç yoktu. Bu API çoğunlukla JSON döndürüyor (bu header'lar orada etkisiz)
// ama /api/subscribe/confirm ve /unsubscribe gerçek HTML sayfası dönüyor —
// e-postadan doğrudan tıklanan bu sayfalar clickjacking/MIME-sniffing gibi
// saldırılara açık olmasın diye. CSP inline style'lara izin veriyor
// (BuildInfoPage stil için bunu kullanıyor) ama script'i tamamen kapatıyor.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'none'; style-src 'unsafe-inline'; img-src 'self'; base-uri 'none'; frame-ancestors 'none'");
    await next();
});

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

// /api/deals, /api/products, /api/store-deals aynı sorgu parametrelerini
// kabul edip sadece onlyDiscounted/onlyStoreDiscounted bayraklarıyla
// ayrışıyordu — üç yerde neredeyse birebir kopya kod yerine tek bir yerden.
void MapDealsQueryEndpoint(string route, bool onlyDiscounted, bool onlyStoreDiscounted)
{
    app.MapGet(route, async (
        DealsQueryService deals, string[]? brands, string[]? categories, string? search,
        decimal? minPrice, decimal? maxPrice, int? days, string? sortBy, int? page, int? pageSize, CancellationToken ct) =>
    {
        var windowDays = days is null or <= 0 ? 30 : days.Value;
        var result = await deals.GetDealsAsync(
            windowDays, brands, categories, search, minPrice, maxPrice,
            onlyDiscounted, onlyStoreDiscounted, sortBy,
            page is null or <= 0 ? 1 : page.Value, NormalizePageSize(pageSize), ct);
        return Results.Ok(result);
    });
}

MapDealsQueryEndpoint("/api/deals", onlyDiscounted: true, onlyStoreDiscounted: false);
MapDealsQueryEndpoint("/api/products", onlyDiscounted: false, onlyStoreDiscounted: false);
// Markanın kendi beyan ettiği (doğrulanmamış) kampanya/indirim fiyatına sahip ürünler.
MapDealsQueryEndpoint("/api/store-deals", onlyDiscounted: false, onlyStoreDiscounted: true);

// Marka karşılaştırma sayfaları için — kategori bazında ortalama fiyat.
app.MapGet("/api/brand-comparison", async (string brand1, string brand2, DealsQueryService deals, CancellationToken ct) =>
{
    var result = await deals.GetBrandComparisonAsync(brand1, brand2, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
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

// Süresi geçen/yanlış çıkan bir kuponu deaktive edebilmek için (Article'daki
// PUT deseniyle aynı) — önceden sadece ekleme vardı, bir kuponu kapatmanın
// API üzerinden hiçbir yolu yoktu.
app.MapPut("/api/dev/coupons/{id:int}", async (int id, UpdateCouponRequest request, CouponService coupons, CancellationToken ct) =>
{
    var result = await coupons.UpdateAsync(id, request, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
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

// Mevcut bir yazıyı düzenlemek için (ör. derinleştirme) — kısmi güncelleme,
// gönderilmeyen alanlar olduğu gibi kalır.
app.MapPut("/api/dev/articles/{slug}", async (string slug, UpdateArticleRequest request, ArticleService articles, CancellationToken ct) =>
{
    var result = await articles.UpdateAsync(slug, request, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
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
    if (!IsValidEmail(request.Email))
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
    if (string.IsNullOrEmpty(request.Token) && !IsValidEmail(request.Email))
        return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

    var (success, token) = await favorites.AddAsync(id, request.Token, request.Email, ct);
    return success ? Results.Ok(new { token }) : Results.NotFound();
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

// E-posta bülteni: double opt-in zorunlu (İYS/KVKK gereği) — bu endpoint
// hiçbir aboneyi doğrudan aktifleştirmiyor, sadece onay maili tetikliyor.
app.MapPost("/api/subscribe", async (SubscribeRequest request, SubscriberService subscribers, HttpContext http, CancellationToken ct) =>
{
    if (!IsValidEmail(request.Email))
        return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

    var confirmBaseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    await subscribers.SubscribeAsync(request, confirmBaseUrl, ct);
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
        ? BuildInfoPage("Aboneliğin onaylandı!", "Artık öne çıkan indirimlerden haberdar olacaksın.", frontendBaseUrl)
        : BuildInfoPage("Bu bağlantı geçersiz.", "Onay linki süresi geçmiş ya da daha önce kullanılmış olabilir.", frontendBaseUrl);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/subscribe/unsubscribe/{token}", async (string token, SubscriberService subscribers, CancellationToken ct) =>
{
    var success = await subscribers.UnsubscribeAsync(token, ct);
    var html = success
        ? BuildInfoPage("Bültenden çıkarıldın.", "Fikrini değiştirirsen tekrar abone olabilirsin.", frontendBaseUrl)
        : BuildInfoPage("Bu bağlantı geçersiz.", "Bağlantı süresi geçmiş ya da daha önce kullanılmış olabilir.", frontendBaseUrl);
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

app.Run();

// Onay/çıkış linklerinin ikisi de aynı markalı kart tasarımını kullanıyor —
// site genelindeki renk paletiyle (brand-600 yeşil, stone nötrleri) tutarlı.
// frontendBaseUrl config'ten geliyor — bu proje domainini bu oturumda 2 kez
// değiştirdi, hardcoded bir adresin unutulup eskide kalması gerçek bir risk.
static string BuildInfoPage(string heading, string message, string frontendBaseUrl) => $"""
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
        <a href="{frontendBaseUrl}" style="display:inline-block;background:#059669;color:#fff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 28px;border-radius:9999px;">Siteye Dön</a>
      </div>
    </body>
    </html>
    """;

// 2026-08-15: sadece "@" içeriyor mu kontrolü, "a;b@x.com" / "a\"b@x.com" gibi
// RFC 5322'ye göre bile geçersiz string'lerin Subscribers tablosuna girmesine
// izin veriyordu (bir güvenlik açığı arayan biri bunları test etmişti — bkz.
// CLAUDE.md). SQL injection zaten EF Core'un parametreli sorguları sayesinde
// mümkün değildi, bu sadece veri hijyeni için — MailAddress'in kendi format
// doğrulamasına güveniyoruz, ayrı bir regex bakımı gerekmiyor.
static bool IsValidEmail(string? email)
{
    if (string.IsNullOrWhiteSpace(email))
        return false;

    try
    {
        return new System.Net.Mail.MailAddress(email).Address == email.Trim();
    }
    catch (FormatException)
    {
        return false;
    }
}

// 2026-08-15 güvenlik denetimi: pageSize'a hiç üst sınır yoktu (ör.
// ?pageSize=5000000 gibi bir istek büyük bir sıralı sorguya yol açabilirdi).
static int NormalizePageSize(int? pageSize) => pageSize is null or <= 0 ? 24 : Math.Min(pageSize.Value, 100);

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

// 2026-08-15 güvenlik olayı sonrası eklendi: e-posta gönderen/yazma yapan
// uçlarda hiç istek logu yoktu, kötüye kullanım olduğunda Render loglarında
// hiçbir iz kalmıyordu. IP + yöntem + yol + zaman `app.Logger` üzerinden
// (Render'ın stdout'u yakaladığı standart kanal) logluyor — ayrı bir log
// servisi/DB tablosu kurmak burada aşırı mühendislik olurdu.
static class RequestLoggingExtensions
{
    public static RouteHandlerBuilder LogSensitiveRequest(this RouteHandlerBuilder builder, ILogger logger)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var ip = GetClientIp(context.HttpContext);
            logger.LogInformation("Hassas istek: {Ip} {Method} {Path}",
                ip, context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return await next(context);
        });
    }

    // 2026-08-15: Render + Cloudflare çift proxy zincirinde RemoteIpAddress
    // (ForwardedHeaders middleware'den sonra bile) Render'ın kendi iç ağındaki
    // bir IP'yi döndürüyordu (10.x.x.x), gerçek ziyaretçi IP'si kayboluyordu —
    // bu da rate limiter'ın ve istek loglarının işe yaramamasına yol açıyordu
    // (tüm istekler aynı "IP" gibi görünüp ortak bir limiti paylaşıyordu).
    // Cloudflare'in CF-Connecting-IP header'ı tam bunun için var — Cloudflare
    // bunu kendi edge'inde üretip origin'e gönderiyor, dışarıdan sahtesi
    // yazılamaz (Cloudflare kendi değerini her zaman ezer). Cloudflare
    // arkasında değilsek (yerel geliştirme) normal RemoteIpAddress'e düşer.
    public static string GetClientIp(HttpContext context)
    {
        var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        return !string.IsNullOrEmpty(cfConnectingIp)
            ? cfConnectingIp
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

record VoteRequest(bool Helpful);

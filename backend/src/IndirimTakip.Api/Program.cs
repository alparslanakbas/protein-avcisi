using Microsoft.Extensions.Options;
using IndirimTakip.Api.Caching;
using IndirimTakip.Api.Endpoints;
using IndirimTakip.Core.Caching;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
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
// 2026-08-18: bu limit "favori ekle" + "kurtarma linki" ikilisini de kapsayacak
// şekilde genişleyince (aynı IP havuzunu paylaşıyorlar) gerçek bir kullanıcı
// normal kullanımda (birkaç favori + kurtarma denemesi) buna takıldı. Asıl
// saldırı koruması zaten AYRI bir katmanda (SubscriberService/FavoriteService'teki
// e-posta bazlı 5 dakikalık cooldown — aynı adrese art arda mail gitmesini
// bağımsız olarak engelliyor), bu yüzden IP limitini 10'a çıkarmak o korumayı
// zayıflatmıyor, sadece normal kullanım için nefes payı veriyor.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("EmailSensitive", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: RequestLoggingExtensions.GetClientIp(httpContext),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
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
const string PublicDataCachePolicy = "PublicData";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

// Sunucu yanıt süresi (TTFB) 27 Ağustos ölçümünde ana sayfada 2,45 sn çıktı;
// sayfayı sunucuda render ederken çağrılan uçlar bu sürenin büyük kısmını
// oluşturuyor. Bu uçların döndürdüğü veri taramalar arasında (altı saat)
// değişmediği için kısa ömürlü bir çıktı önbelleği tazelikten ödün vermeden
// tekrarlanan sorguları ortadan kaldırıyor.
//
// Varsayılan politika bilinçli olarak "önbellekleme yok": böylece bir uç,
// açıkça işaretlenmediği sürece önbelleğe girmiyor. Kişiye özel yanıt
// döndüren uçlar (favori listesi, abonelik onayı) ve sayaç artıran /go/{id}
// bu yüzden kazara önbelleğe alınamaz.
// 60 saniyeydi; 3 Eylül'de ölçüldü ki bu sürede pratikte HER ziyaretçi soğuk
// önbelleğe düşüyor (trafik düşük): /api/deals sıcakta 0,26 sn, soğukta 2,1 sn;
// ana sayfa soğukta 6,0 sn. Veri yalnızca taramayla değiştiği için süre uzun
// tutuluyor ve tarama biter bitmez ETİKETE GÖRE temizleniyor
// (OutputCacheRefresher) — böylece hem soğuk isabet kalmıyor hem de veri
// hiçbir zaman bir taramadan daha bayat olmuyor.
var publicCacheSeconds = builder.Configuration.GetValue("OutputCache:PublicSeconds", 3600);
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.NoCache());
    options.AddPolicy(PublicDataCachePolicy, policy => policy
        .Expire(TimeSpan.FromSeconds(publicCacheSeconds))
        // Tarama sonrası toplu temizleme bu etikete göre yapılıyor.
        .Tag(OutputCacheRefresher.Tag)
        // Filtre/sayfalama parametreleri yanıtı tamamen değiştiriyor; tümü
        // önbellek anahtarına dahil edilmezse farklı filtreler birbirinin
        // sonucunu görürdü.
        .SetVaryByQuery("*"));
});

// Tarama bitince önbelleği tazeleyen uygulama. Arayüz Core'da: scraper'ların
// HTTP sunucusundan haberi olmasın diye somut tip buraya bırakıldı.
builder.Services.AddHttpClient(nameof(OutputCacheRefresher), client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IPublicCacheRefresher, OutputCacheRefresher>();

builder.Services.Configure<AffiliateOptions>(builder.Configuration.GetSection("Affiliate"));

var app = builder.Build();

// Geçici /api/dev/* endpoint'leri (elle tarama tetikleme, kupon ekleme) canlıya
// çıktıktan sonra da korumasız kaldı — herkes bu uçlara istek atıp taramayı
// tetikleyebilir ya da sahte kupon ekleyebilirdi. Tam bir kullanıcı/auth sistemi
// kurmak bu iki endpoint için aşırı mühendislik; basit bir paylaşılan anahtar
// (header'da) yeterli. Anahtar ayarlanmamışsa (ör. yerelde unutulduysa) güvenli
// tarafta kalıp erişimi tamamen reddediyoruz.
var adminApiKey = app.Configuration["AdminApiKey"];
// Dışarıda toplanan ürünleri kabul eden uç için AYRI anahtar — bkz.
// CollectorEndpoints. Admin anahtarı kullanılmıyor, çünkü bu değer
// geliştirme makinesinde de duracak.
var ingestApiKey = app.Configuration["IngestApiKey"];

// Onay/abonelikten çıkma sayfalarındaki "Siteye Dön" linki ve bültendeki
// ürün/site linkleri için — tek yerden yönetiliyor ki domain değişince
// (bu proje bu oturumda bile 2 kez değiştirdi) unutulan bir yer kalmasın.
var frontendBaseUrl = app.Configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr";
var frontendImageSource = Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri)
    && frontendUri.Scheme is "http" or "https"
        ? frontendUri.GetLeftPart(UriPartial.Authority)
        : null;

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
        var error = exceptionFeature?.Error;

        // BadHttpRequestException, ASP.NET Core'un query/route/body binding
        // sırasında (ör. ?days=abc gibi int'e çevrilemeyen bir query değeri)
        // fırlattığı, istemci kaynaklı bir hata — bunu genel 500'e düşürmek
        // yerine kendi durum kodunu (genelde 400) koruyoruz, sunucu hatası
        // gibi ERROR seviyesinde de loglamıyoruz. TestSprite'ın otomatik
        // testinde yakalandı (bkz. CLAUDE.md).
        if (error is BadHttpRequestException badRequest)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = badRequest.StatusCode;
            await context.Response.WriteAsJsonAsync(new { message = "Geçersiz istek." });
            return;
        }

        if (error is { } ex)
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
// Çıktı önbelleği CORS'tan SONRA gelmeli — aksi halde önbellekten dönen yanıtta
// CORS başlıkları eksik kalabilir. Hız sınırlayıcıdan da sonra gelmesi bilinçli:
// önbellekten karşılanan istekler de sayılmaya devam ediyor.
app.UseRateLimiter();
app.UseOutputCache();

// 2026-08-15 güvenlik denetimi: tarayıcı seviyesinde ek bir savunma katmanı
// hiç yoktu. Bu API çoğunlukla JSON döndürüyor (bu header'lar orada etkisiz)
// ama /api/subscribe/confirm ve /unsubscribe gerçek HTML sayfası dönüyor —
// e-postadan doğrudan tıklanan bu sayfalar clickjacking/MIME-sniffing gibi
// saldırılara açık olmasın diye. CSP inline style'lara izin veriyor
// (BuildInfoPage stil için bunu kullanıyor) ama script'i tamamen kapatıyor.
// Başarılı abonelik onay sayfasındaki görseller frontend domaininden geliyor;
// bu origin açıkça izinli değilse tarayıcı dosyalar 200 dönse bile hepsini CSP
// nedeniyle engelliyor. Config değeri Uri ile ayrıştırılarak yalnızca güvenli
// http/https origin'i ekleniyor; ham config metni header'a taşınmıyor.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    var imageSources = frontendImageSource is null
        ? "'self'"
        : $"'self' {frontendImageSource}";
    context.Response.Headers.Append("Content-Security-Policy",
        $"default-src 'none'; style-src 'unsafe-inline'; img-src {imageSources}; base-uri 'none'; frame-ancestors 'none'");
    await next();
});

// Uçlar konularına göre ayrı dosyalarda (Endpoints/). Program.cs 1.216
// satıra çıkmıştı ve 42 uç tek dosyadaydı; bu bölme yalnızca organizasyon —
// rotalar, önbellek politikaları, hız sınırları ve filtreler birebir aynı.
app.MapAdminEndpoints(adminApiKey);
app.MapCollectorEndpoints(ingestApiKey);
app.MapDealsEndpoints(PublicDataCachePolicy);
app.MapCouponEndpoints(PublicDataCachePolicy);
app.MapArticleEndpoints(PublicDataCachePolicy);
app.MapPriceHistoryEndpoints(PublicDataCachePolicy);
app.MapEngagementEndpoints(frontendBaseUrl);
app.MapStoreRedirectEndpoints();
app.MapSubscriptionEndpoints(frontendBaseUrl);


app.Run();

using System.Security.Cryptography;
using System.Text;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.Supplementler;

namespace IndirimTakip.Api.Endpoints;

/// <summary>
/// DIŞARIDA TOPLANMIŞ ürünleri kabul eden uç.
///
/// <b>NEDEN GEREKLİ.</b> Supplementler.com, sunucumuzun bulunduğu datacenter
/// aralığından Cloudflare <i>managed challenge</i> ile karşılıyor: 403'ün
/// gövdesi "Just a moment..." sayfası (<c>cType: 'managed'</c>), yani düz bir
/// IP engeli değil, JS çalıştırmayı bekleyen bir doğrulama. Ev bağlantısından
/// aynı adres normal 200 dönüyor (4 Eylül'de ölçüldü). Bu yüzden bu tek
/// kaynakta TOPLAMA geliştirme makinesinde çalışıyor, sonucu buraya
/// gönderiyor; yutma mantığı sunucuda ve diğer 34 kaynakla aynı kod.
///
/// <b>SINIRLAR — bilerek dar tutuldu.</b> Bu uç veritabanına yazıyor, yani
/// yeni bir saldırı yüzeyi:
/// <list type="bullet">
/// <item>Kendi anahtarı var (<c>IngestApiKey</c>), admin anahtarı DEĞİL.
/// Anahtar artık geliştirme makinesinde de duracağı için, sızması hâlinde
/// admin uçlarına erişim vermemeli. Karşılaştırma sabit zamanlı.</item>
/// <item>Yalnızca beyaz listedeki kaynak adları kabul ediliyor; gövde
/// istediği markayı yazamaz, marka çözümlemesi her zamanki gibi
/// <c>ScrapeIngestionService</c> içinde yapılıyor.</item>
/// <item>Ürün sayısı tavanlı: kaynak canlıda ~545 ürün veriyor, 5000 hem
/// rahat pay bırakıyor hem şişirilmiş gövdeyi kesiyor.</item>
/// </list>
/// </summary>
internal static class CollectorEndpoints
{
    /// <summary>
    /// Dışarıdan gönderim kabul eden kaynaklar. Beyaz liste: burada olmayan
    /// bir ad 404 alır.
    /// </summary>
    private static readonly Dictionary<string, Type> AllowedSources =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["supplementler"] = typeof(SupplementlerScraper),
        };

    private const int MaxProducts = 5000;

    public static void MapCollectorEndpoints(this WebApplication app, string? ingestApiKey)
    {
        var logger = app.Logger;

        app.MapPost("/api/ingest/{source}", async (
            string source,
            ScrapedProduct[] products,
            IServiceProvider services,
            ScrapeIngestionService ingestion,
            CancellationToken ct) =>
        {
            if (!AllowedSources.TryGetValue(source, out var scraperType))
                return Results.NotFound($"'{source}' dışarıdan gönderim için tanımlı değil.");

            if (products.Length == 0)
                return Results.BadRequest("Ürün listesi boş.");

            if (products.Length > MaxProducts)
                return Results.BadRequest($"Ürün sayısı tavanı aşıldı ({products.Length} > {MaxProducts}).");

            // Boş gövdeli/bozuk kayıtlar yutma katmanına hiç girmemeli.
            if (products.Any(p => string.IsNullOrWhiteSpace(p.Name)
                               || string.IsNullOrWhiteSpace(p.Url)
                               || p.Price <= 0))
            {
                return Results.BadRequest("Adı, adresi veya fiyatı geçersiz kayıt var.");
            }

            if (services.GetService(scraperType) is not IBrandScraper scraper)
                return Results.Problem($"'{source}' için scraper çözümlenemedi.");

            try
            {
                var count = await ingestion.IngestAsync(scraper, products, ct);
                logger.LogInformation(
                    "Dışarıdan gönderim yutuldu: {Source}, {Count} ürün.", source, count);
                return Results.Ok(new { source, ingested = count });
            }
            catch (InvalidOperationException ex)
            {
                // Aynı kaynağın taraması zaten çalışıyor.
                logger.LogWarning("Dışarıdan gönderim reddedildi: {Message}", ex.Message);
                return Results.Conflict(ex.Message);
            }
        })
        .RequireIngestKey(ingestApiKey)
        .LogSensitiveRequest(logger)
        .RequireRateLimiting("EmailSensitive");
    }
}

internal static class IngestAuthExtensions
{
    /// <summary>
    /// <c>X-Ingest-Key</c> başlığını sabit zamanlı karşılaştırır.
    ///
    /// Sabit zamanlı olması bir detay değil: bu anahtar artık sunucunun
    /// dışında, geliştirme makinesinde de duruyor ve uç internete açık.
    /// Sıradan <c>!=</c> karşılaştırması ilk farklı bayta kadar sürer ve
    /// yeterli örneklemeyle anahtarın bayt bayt tahmin edilmesine kapı
    /// aralar.
    /// </summary>
    public static RouteHandlerBuilder RequireIngestKey(this RouteHandlerBuilder builder, string? expectedKey)
    {
        var expected = string.IsNullOrEmpty(expectedKey) ? null : Encoding.UTF8.GetBytes(expectedKey);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            // Anahtar tanımlı değilse uç tamamen kapalı — yapılandırma
            // eksikliği "herkese açık" anlamına GELMEMELİ.
            if (expected is null)
                return Results.Unauthorized();

            var provided = context.HttpContext.Request.Headers["X-Ingest-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(provided))
                return Results.Unauthorized();

            var providedBytes = Encoding.UTF8.GetBytes(provided);
            if (!CryptographicOperations.FixedTimeEquals(providedBytes, expected))
                return Results.Unauthorized();

            return await next(context);
        });
    }
}

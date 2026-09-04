using System.Net.Http.Json;
using System.Text.Json;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.Supplementler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
// ProteinAvcısı — dışarıdan toplayıcı
//
// NEDEN VAR: Supplementler.com sunucumuzun bulunduğu datacenter aralığından
// Cloudflare managed challenge'ı ile karşılanıyor (403 gövdesi "Just a
// moment..." sayfası, cType: 'managed'). Ev bağlantısından aynı adres normal
// 200 dönüyor — 4 Eylül'de iki taraftan da ölçüldü. Bu yüzden bu TEK kaynağın
// toplaması geliştirme makinesinde çalışıyor, sonucu API'ye gönderiyor.
//
// KENDİNİ TANITIYOR: tarayıcı taklidi yapılmıyor. Sitenin robots.txt'i
// okunan yolu (/c/{slug}-{id}) yasaklamıyor — yasakladıkları /urunler/,
// /markalar/, /shop/, /Catalog/, /m/ — ve dürüst bir bot User-Agent'ı ile de
// 200 dönüyor (ölçüldü). Böylece site isterse bizi User-Agent'a bakarak
// engelleyebilir; gizlenmiyoruz.
//
// Kullanım:
//   IndirimTakip.Toplayici.exe supplementler
// Ortam değişkenleri:
//   PA_INGEST_URL  (ör. https://api.proteinavcisi.com.tr)
//   PA_INGEST_KEY  (sunucudaki IngestApiKey ile aynı)
//   PA_KURU_CALIS  ("1" ise gönderim yapılmaz, yalnızca toplanır ve raporlanır)
// ---------------------------------------------------------------------------

const string UserAgent =
    "Mozilla/5.0 (compatible; ProteinAvcisiBot/1.0; +https://www.proteinavcisi.com.tr)";

var kaynak = args.FirstOrDefault() ?? "supplementler";
var kuruCalis = Environment.GetEnvironmentVariable("PA_KURU_CALIS") == "1";
var ingestUrl = Environment.GetEnvironmentVariable("PA_INGEST_URL")?.TrimEnd('/');
var ingestKey = Environment.GetEnvironmentVariable("PA_INGEST_KEY");

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; })
    .SetMinimumLevel(LogLevel.Information));
var log = loggerFactory.CreateLogger("Toplayici");

if (!kaynak.Equals("supplementler", StringComparison.OrdinalIgnoreCase))
{
    log.LogError("Bilinmeyen kaynak: {Kaynak}. Tanımlı olan: supplementler.", kaynak);
    return 2;
}

if (!kuruCalis && (string.IsNullOrWhiteSpace(ingestUrl) || string.IsNullOrWhiteSpace(ingestKey)))
{
    log.LogError("PA_INGEST_URL ve PA_INGEST_KEY tanımlı değil. "
               + "Yalnızca toplayıp görmek için PA_KURU_CALIS=1 kullanılabilir.");
    return 2;
}

var services = new ServiceCollection();
services.AddSingleton<ILoggerFactory>(loggerFactory);
services.AddLogging();
services.AddHttpClient<SupplementlerScraper>(client =>
{
    client.BaseAddress = new Uri("https://www.supplementler.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    // Toplama ~4 dakika sürüyor ama tek istek asla o kadar sürmemeli.
    client.Timeout = TimeSpan.FromSeconds(60);
});

await using var provider = services.BuildServiceProvider();
var scraper = provider.GetRequiredService<SupplementlerScraper>();

// Görev zamanlayıcıdan çalışırken sonsuza kadar asılı kalmasın.
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

IReadOnlyList<ScrapedProduct> urunler;
try
{
    log.LogInformation("Toplama başladı: {Kaynak}.", kaynak);
    urunler = await scraper.ScrapeAsync(cts.Token);
}
catch (OperationCanceledException)
{
    log.LogError("Toplama 20 dakikada bitmedi, iptal edildi.");
    return 1;
}
catch (Exception ex)
{
    log.LogError(ex, "Toplama başarısız.");
    return 1;
}

log.LogInformation("Toplandı: {Adet} ürün.", urunler.Count);

// Boş sonuç GÖNDERİLMİYOR. Challenge'a takılan bir tur sıfır ürün döndürür;
// bunu göndermek sunucudaki 545 ürünün hepsini bir anda "bayat" yapardı.
if (urunler.Count == 0)
{
    log.LogError("Sıfır ürün toplandı — gönderim yapılmadı. "
               + "Muhtemel sebep: Cloudflare challenge ya da site yapısı değişti.");
    return 1;
}

if (kuruCalis)
{
    log.LogInformation("Kuru çalışma: gönderim yapılmadı. İlk üç kayıt:");
    foreach (var u in urunler.Take(3))
        log.LogInformation("  {Ad} | {Fiyat} TL | {Marka}", u.Name, u.Price, u.BrandName ?? "-");
    return 0;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
http.DefaultRequestHeaders.Add("X-Ingest-Key", ingestKey);

try
{
    var yanit = await http.PostAsJsonAsync(
        $"{ingestUrl}/api/ingest/{kaynak.ToLowerInvariant()}", urunler, cts.Token);

    var govde = await yanit.Content.ReadAsStringAsync(cts.Token);
    if (!yanit.IsSuccessStatusCode)
    {
        log.LogError("Gönderim başarısız: {Durum} {Govde}", (int)yanit.StatusCode, govde);
        return 1;
    }

    log.LogInformation("Gönderildi: {Govde}", govde);
    return 0;
}
catch (Exception ex)
{
    log.LogError(ex, "Gönderim sırasında hata.");
    return 1;
}

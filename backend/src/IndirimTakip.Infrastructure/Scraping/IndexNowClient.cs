using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

// IndexNow: Bing, Yandex ve Seznam'ın ortak bildirim protokolü. Bir sayfa
// değiştiğinde arama motorunun bizi yeniden taramasını beklemek yerine
// doğrudan haber veriyoruz.
//
// Neden eklendi (2026-08-28): beş yapay zekâ modeline "protein indirimlerini
// gösteren siteler var mı" diye soruldu, üçü siteyi HİÇ bulamadı. Araştırınca
// sebep çıktı — Google'da 393 sayfa dizinliyken **Bing'de pratikte hiç
// dizinli değiliz** (site: sorgusu boş, marka araması sıfır sonuç,
// DuckDuckGo sıfır). Teknik bir engel yok: Bingbot ana sayfayı ve site
// haritasını sorunsuz çekiyor, robots.txt kimseyi engellemiyor. Site
// haritası gönderilmiş ama işlenmemiş — yeni ve dışarıdan bağlantı almayan
// siteler için bilinen bir davranış. Yapay zekâ araçlarının çoğu Bing
// dizinini kullandığı için bu, doğrudan bir görünürlük sorunu.
//
// Anahtar gizli bir değer DEĞİL: site sahipliğini doğrulamak için
// `https://{host}/{key}.txt` adresinde herkese açık yayınlanıyor.
public class IndexNowClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<IndexNowClient> logger)
{
    // Protokol tek istekte en fazla 10.000 adres kabul ediyor.
    private const int MaxUrlsPerRequest = 10_000;

    public bool IsEnabled =>
        configuration.GetValue("IndexNow:Enabled", true)
        && !string.IsNullOrWhiteSpace(configuration["IndexNow:Key"]);

    /// <summary>Verilen adresleri arama motorlarına bildirir. Gönderilen adres sayısını döndürür.</summary>
    public async Task<int> SubmitAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken = default)
    {
        if (urls.Count == 0) return 0;

        var key = configuration["IndexNow:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogInformation("IndexNow anahtarı tanımlı değil, bildirim atlandı.");
            return 0;
        }

        if (!configuration.GetValue("IndexNow:Enabled", true))
            return 0;

        var frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr").TrimEnd('/');
        var host = new Uri(frontendBaseUrl).Host;

        var sent = 0;
        foreach (var batch in urls.Chunk(MaxUrlsPerRequest))
        {
            var payload = new
            {
                host,
                key,
                keyLocation = $"{frontendBaseUrl}/{key}.txt",
                urlList = batch,
            };

            try
            {
                var response = await httpClient.PostAsJsonAsync("https://api.indexnow.org/indexnow", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    sent += batch.Length;
                    logger.LogInformation("IndexNow: {Count} adres bildirildi ({Status}).", batch.Length, (int)response.StatusCode);
                }
                else
                {
                    // 403 = anahtar doğrulanamadı, 422 = adres host ile uyuşmuyor,
                    // 429 = çok sık istek. Hiçbiri taramayı durduracak bir hata değil.
                    logger.LogWarning(
                        "IndexNow bildirimi reddedildi: {Status}. Gönderilen adres sayısı: {Count}.",
                        (int)response.StatusCode, batch.Length);
                }
            }
            catch (Exception ex)
            {
                // Bildirim ikincil bir iş — başarısız olması taramayı ya da
                // çağıran akışı asla bozmamalı.
                logger.LogWarning(ex, "IndexNow bildirimi gönderilemedi.");
            }
        }

        return sent;
    }
}

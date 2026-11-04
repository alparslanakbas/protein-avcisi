using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Shopify kataloğunun bir sayfası (<c>products.json</c>); 429'da yanıtın <c>Retry-After</c> süresi kadar
/// bekleyip bir kez daha deniyor.
/// </summary>
/// <remarks>
/// Çekiciler 429'da hata atıyor ve mağaza o turu kaybediyordu: 30 Ekim-1 Kasım'da Commander 7 turun 5'inde
/// atlandı, 26 saatlik sağlık eşiğine yaklaştı. Bir dakikalık bekleme ilk 4 turda 4 denemenin 4'ünü kurtardı.
///
/// <b>Retry-After (3 Kasım 2026).</b> Shopify'ın bot incelemesi sabit süre yerine bu başlıktaki süre kadar
/// beklememizi istedi (destek bileti 71441774). Başlık yoksa bir dakika; <see cref="EnUzunBekleme"/>'den
/// uzun süre isteyen mağaza hiç denenmiyor, sıradaki tura (saatler sonra) kalıyor, arkadaki mağazaları
/// bekletmiyor.
///
/// Bekleme işleyici zincirinde değil burada: HttpClient.Timeout zincirin tamamını kapsıyor ve Gigi's, Fellas,
/// Bahs istemcilerinde 30 saniye, bekleme orada isteği düşürürdü. İkinci deneme de 429 verirse hata eskisi
/// gibi yukarı çıkıyor: mağaza o turu kaybediyor, eldeki fiyatlar yerinde kalıyor.
///
/// <b>Sayfa arası (4 Kasım 2026).</b> Bot sayfamız (wheyproof.com/bot, Shopify kaydında belge adresi)
/// mağazaya tek tek ve sayfalar arasında bekleyerek gittiğimizi söylüyor. WheyProof 1 sn, kahve sitesi 3 sn
/// bekliyordu, buradaki yedi mağaza robots.txt Crawl-delay istemedikçe beklemiyordu: ikinci sayfadan önce
/// WheyProof'la aynı süre. Bekleme isteğin dışında, aynı gerekçeyle (HttpClient.Timeout'a sayılmasın).
/// </remarks>
internal static class ShopifyKatalog
{
    internal static readonly TimeSpan VarsayilanBekleme = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan EnUzunBekleme = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan SayfaArasi = TimeSpan.FromSeconds(1);

    internal static async Task<ShopifyProductsResponse?> SayfaGetirAsync(
        HttpClient httpClient, int sayfa, JsonSerializerOptions secenekler, string marka, ILogger? logger,
        CancellationToken iptal, TimeSpan? bekleme = null, TimeSpan? sayfaArasi = null)
    {
        if (sayfa > 1)
            await Task.Delay(sayfaArasi ?? SayfaArasi, iptal);

        var adres = $"products.json?limit=250&page={sayfa}";
        using (var ilk = await httpClient.GetAsync(adres, iptal))
        {
            if (ilk.StatusCode != HttpStatusCode.TooManyRequests)
                return await OkuAsync(ilk, secenekler, iptal);

            var sure = BeklemeSuresi(ilk, bekleme ?? VarsayilanBekleme, DateTimeOffset.UtcNow);
            if (sure > EnUzunBekleme)
            {
                logger?.LogInformation("{Marka}: katalog {Sayfa}. sayfada 429 verdi; Retry-After {Sure} istiyor, sıradaki tura kaldı.",
                    marka, sayfa, sure);
                throw new HttpRequestException("En uzun beklemeyi aşan 429.", null, HttpStatusCode.TooManyRequests);
            }

            logger?.LogInformation("{Marka}: katalog {Sayfa}. sayfada 429 verdi; {Sure} sonra bir kez daha deneniyor ({Kaynak}).",
                marka, sayfa, sure, ilk.Headers.RetryAfter is null ? "Retry-After yok" : "Retry-After");
            await Task.Delay(sure, iptal);
        }

        using var ikinci = await httpClient.GetAsync(adres, iptal);
        return await OkuAsync(ikinci, secenekler, iptal);
    }

    /// <summary>429'un istediği bekleme (saniye ya da tarih); başlık yoksa <paramref name="yoksa"/>.</summary>
    internal static TimeSpan BeklemeSuresi(HttpResponseMessage yanit, TimeSpan yoksa, DateTimeOffset simdi)
    {
        var baslik = yanit.Headers.RetryAfter;
        var sure = baslik?.Delta ?? (baslik?.Date is { } tarih ? tarih - simdi : yoksa);
        return sure < TimeSpan.Zero ? TimeSpan.Zero : sure;
    }

    private static async Task<ShopifyProductsResponse?> OkuAsync(HttpResponseMessage yanit,
        JsonSerializerOptions secenekler, CancellationToken iptal)
    {
        yanit.EnsureSuccessStatusCode();
        return await yanit.Content.ReadFromJsonAsync<ShopifyProductsResponse>(secenekler, iptal);
    }
}

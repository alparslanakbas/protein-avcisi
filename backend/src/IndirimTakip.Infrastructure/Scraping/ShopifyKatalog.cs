using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Shopify kataloğunun bir sayfası (<c>products.json</c>); 429'da bir dakika sonra bir kez daha deniyor.
/// </summary>
/// <remarks>
/// Çekiciler 429'da hata atıyor ve mağaza o turu kaybediyordu: 30 Ekim-1 Kasım'da Commander 7 turun 5'inde
/// atlandı, 26 saatlik sağlık eşiğine yaklaştı. Aynı bekleme WheyProof'ta 24 saatte 67 denemenin 58'ini
/// kurtardı (1 Kasım); sınır genellikle bir dakikada geçiyor.
///
/// Bekleme işleyici zincirinde değil burada: HttpClient.Timeout zincirin tamamını kapsıyor ve Gigi's, Fellas,
/// Bahs istemcilerinde 30 saniye, bekleme orada isteği düşürürdü. İkinci deneme de 429 verirse hata eskisi
/// gibi yukarı çıkıyor: mağaza o turu kaybediyor, eldeki fiyatlar yerinde kalıyor.
/// </remarks>
internal static class ShopifyKatalog
{
    internal static readonly TimeSpan VarsayilanBekleme = TimeSpan.FromMinutes(1);

    internal static async Task<ShopifyProductsResponse?> SayfaGetirAsync(
        HttpClient httpClient, int sayfa, JsonSerializerOptions secenekler, string marka, ILogger? logger,
        CancellationToken iptal, TimeSpan? bekleme = null)
    {
        var adres = $"products.json?limit=250&page={sayfa}";
        try
        {
            return await httpClient.GetFromJsonAsync<ShopifyProductsResponse>(adres, secenekler, iptal);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var sure = bekleme ?? VarsayilanBekleme;
            logger?.LogInformation("{Marka}: katalog {Sayfa}. sayfada 429 verdi; {Sure} sonra bir kez daha deneniyor.",
                marka, sayfa, sure);
            await Task.Delay(sure, iptal);
            return await httpClient.GetFromJsonAsync<ShopifyProductsResponse>(adres, secenekler, iptal);
        }
    }
}

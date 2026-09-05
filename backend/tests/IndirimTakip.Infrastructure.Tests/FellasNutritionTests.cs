using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.Fellas;

namespace IndirimTakip.Infrastructure.Tests;

// fellasfoods.com.tr'deki gerçek ürün sayfalarından (2026-09-05) alınmış
// parçalar. Fellas'ta besin tablosu Shopify body_html'inde DEĞİL — kataloğun
// tamamı (123 ürün) tarandı, hiçbirinde yok; blok yalnızca ürün sayfasında
// basılıyor. Bu yüzden ayrı bir detay isteği gerekiyor.
public class FellasNutritionTests
{
    // "yaklaşık 10 porsiyondur" bilgisi OLAN biçim (fıstık/yulaf ezmesi gibi
    // çok porsiyonlu ürünler).
    private const string CokPorsiyonlu = """
        <html><body>
        <div class="nutrition-box">
          <h2>Besin Değerleri</h2>
          <p class="nutrition-info">Değerler 1 porsiyon (30 g) içindir. 1 paket (300 g) yaklaşık 10 porsiyondur.</p>
          <div class="nutrition-large">99.0 kcal</div>
          <div class="nutrition-row nutrition-bold"><span>Toplam Yağ</span><span>0.9 g</span></div>
          <div class="nutrition-row nutrition-sub"><span>Doymuş Yağ</span><span>0.2 g</span></div>
          <div class="nutrition-row nutrition-bold"><span>Toplam Karbonhidrat</span><span>19.0 g</span></div>
          <div class="nutrition-row nutrition-sub"><span>Diyet Lifi</span><span>3.3 g</span></div>
          <div class="nutrition-row nutrition-bold"><span>Protein</span><span>3.5 g</span></div>
        </div>
        </body></html>
        """;

    // Tek porsiyonluk ürün (bar): porsiyon SAYISI cümlede hiç geçmiyor.
    private const string TekPorsiyonluk = """
        <html><body>
        <div class="nutrition-box">
          <p class="nutrition-info">Bir tüketim birimi 1 pakettir. Değerler 1 paket (50 g) içindir.</p>
          <div class="nutrition-row nutrition-bold"><span>Protein</span><span>20 g</span></div>
        </div>
        </body></html>
        """;

    private static FellasScraper Scraper(string html) =>
        new(new HttpClient(new SabitYanitHandler(html)) { BaseAddress = new Uri("https://fellasfoods.com.tr") });

    [Fact]
    public async Task BesinSatirlariOkunuyor()
    {
        var d = await Scraper(CokPorsiyonlu).FetchDetailsAsync("https://fellasfoods.com.tr/products/x");

        var tablo = JsonSerializer.Deserialize<Dictionary<string, string>>(d.NutritionJson!)!;
        Assert.Equal("19.0 g", tablo["Toplam Karbonhidrat"]);
        Assert.Equal("3.5 g", tablo["Protein"]);
        Assert.Equal(3.5m, d.ProteinPerServingGrams);
    }

    [Fact]
    public async Task PorsiyonBuyuklugu_PaketDegil_PorsiyonOlarakOkunuyor()
    {
        var d = await Scraper(CokPorsiyonlu).FetchDetailsAsync("https://fellasfoods.com.tr/products/x");

        // Cümlede hem 30 g (porsiyon) hem 300 g (paket) geçiyor; porsiyon
        // olan alınmalı, yoksa servis başı fiyat on kat yanlış çıkardı.
        Assert.Equal(30m, d.ServingSizeGrams);
        Assert.Equal(10, d.ServingsPerPackage);
    }

    // ASIL TUZAK: cümle "Değerler 1 porsiyon…" diye başlıyor. Metindeki ilk
    // sayıyı alan genel bir okuyucu her üründe 1 servis derdi ve servis başı
    // fiyat paketin tamamına eşitlenirdi.
    [Fact]
    public async Task CumledekiIlkSayiPorsiyonSayisiSanilmiyor()
    {
        var d = await Scraper(TekPorsiyonluk).FetchDetailsAsync("https://fellasfoods.com.tr/products/y");

        Assert.Equal(50m, d.ServingSizeGrams);
        Assert.Null(d.ServingsPerPackage);
    }

    // Çoklu paketler ve shaker gibi ürünlerde blok hiç basılmıyor — ölçüldü,
    // 12 üründe 4'ü böyle. Boş sonuç hata değil, uydurma veri üretilmiyor.
    [Fact]
    public async Task BesinBloguYoksaNullDonuyor()
    {
        var d = await Scraper("<html><body><div>Shaker 450ml</div></body></html>")
            .FetchDetailsAsync("https://fellasfoods.com.tr/products/shaker-450ml");

        Assert.Null(d.NutritionJson);
        Assert.Null(d.ServingSizeGrams);
        Assert.Null(d.ServingsPerPackage);
    }

    [Fact]
    public async Task AciklamaCekilmiyor()
    {
        var d = await Scraper(CokPorsiyonlu).FetchDetailsAsync("https://fellasfoods.com.tr/products/x");
        Assert.Null(d.Description);
    }

    private sealed class SabitYanitHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }
}

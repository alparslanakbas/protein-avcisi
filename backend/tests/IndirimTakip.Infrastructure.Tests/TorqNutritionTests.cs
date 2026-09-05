using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.Torq;

namespace IndirimTakip.Infrastructure.Tests;

// Torq'a uzun süre detay çekicisi YAZILMAMIŞTI ve gerekçe kodda yazılıydı:
// "açıklama tablosu boş geliyor". 5 Eylül'de yeniden ölçüldü — gerekçe
// yalnızca AÇIKLAMA için doğruymuş; besin değeri sunucudan gelen HTML'de
// eksiksiz duruyor. Aşağıdaki parça torqnutrition.com.tr'deki gerçek bir
// ürün sayfasından (p80-whey-protein, 2026-09-05) alınmıştır.
public class TorqNutritionTests
{
    private const string GercekSayfaParcasi = """
        <html><body>
        <div class="bbilgileri_ic">
          <div class="bbilgi_baslik">Besin Bilgileri</div>
          <div class="ust_bilgiler"><span class="baslik">Porsiyon Büyüklüğü:</span><span class="deger">30 Gram</span></div>
          <div class="ust_bilgiler"><span class="baslik">Porsiyon Sayısı:</span><span class="deger">30 Servis</span></div>
          <div class="herporsiyon">Her Porsiyon İçin Miktar</div>
          <div class="satirlar"><span class="baslik">Enerji / Energy </span> <span class="deger">113 kcal/472 kj</span> </div>
          <div class="satirlar"><span class="baslik">Yağ / Fat</span> <span class="deger">1,2 gr</span> </div>
          <div class="satirlar"><span class="baslik">Doymuş Yağ / Saturated Fat</span> <span class="deger">0,9 gr</span> </div>
          <div class="satirlar"><span class="baslik">Karbonhidrat / Carbohydrate</span> <span class="deger">1,5 gr</span> </div>
          <div class="satirlar"><span class="baslik">Şeker / Sugar</span> <span class="deger">1,5 gr</span> </div>
          <div class="satirlar"><span class="baslik">Protein / Protein</span> <span class="deger">24 gr</span> </div>
        </div>
        </body></html>
        """;

    private static TorqScraper Scraper(string html) =>
        new(new HttpClient(new SabitYanitHandler(html))
        {
            BaseAddress = new Uri("https://www.torqnutrition.com.tr"),
        });

    [Fact]
    public async Task BesinSatirlariOkunuyor()
    {
        var d = await Scraper(GercekSayfaParcasi).FetchDetailsAsync("https://www.torqnutrition.com.tr/p80-whey-protein");

        Assert.NotNull(d.NutritionJson);
        var tablo = JsonSerializer.Deserialize<Dictionary<string, string>>(d.NutritionJson!)!;

        Assert.Equal("113 kcal/472 kj", tablo["Enerji / Energy"]);
        Assert.Equal("1,5 gr", tablo["Karbonhidrat / Carbohydrate"]);
        // "Protein / Protein" tekrar eden etiket — NutritionParser tek parçaya indiriyor.
        Assert.Equal("24 gr", tablo["Protein"]);
    }

    [Fact]
    public async Task ProteinGramiCikariliyor()
    {
        var d = await Scraper(GercekSayfaParcasi).FetchDetailsAsync("https://www.torqnutrition.com.tr/p80-whey-protein");
        Assert.Equal(24m, d.ProteinPerServingGrams);
    }

    // "30 Gram" ve "30 Servis" — birimler BigJoy'dakinden ("32g", "68")
    // farklı yazılıyor, ortak ayrıştırıcı ikisini de tanımalı.
    [Fact]
    public async Task PorsiyonBilgisiOkunuyor()
    {
        var d = await Scraper(GercekSayfaParcasi).FetchDetailsAsync("https://www.torqnutrition.com.tr/p80-whey-protein");

        Assert.Equal(30m, d.ServingSizeGrams);
        Assert.Equal(30, d.ServingsPerPackage);
    }

    // Sınıf yorumundaki eski ölçüm hâlâ geçerli: açıklama sunucu HTML'inde
    // yok. Uydurma bir açıklama üretmek yerine null dönülüyor.
    [Fact]
    public async Task AciklamaCekilmiyor()
    {
        var d = await Scraper(GercekSayfaParcasi).FetchDetailsAsync("https://www.torqnutrition.com.tr/p80-whey-protein");
        Assert.Null(d.Description);
    }

    [Fact]
    public async Task BesinBolumuYoksaNullDonuyor()
    {
        var d = await Scraper("<html><body><div>Shaker</div></body></html>")
            .FetchDetailsAsync("https://www.torqnutrition.com.tr/shaker");

        Assert.Null(d.NutritionJson);
        Assert.Null(d.ServingSizeGrams);
        Assert.Null(d.ServingsPerPackage);
    }

    private sealed class SabitYanitHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }
}

using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.BigJoy;

namespace IndirimTakip.Infrastructure.Tests;

// Bu testlerin varlık sebebi bir ÖLÇÜM (5 Eylül): katalogda besin değeri
// olan ürün 4.918'de 328'di (%6,7) ve eksiklerin bir kısmı "kaynakta veri
// yok" değil, "kaynak <table> kullanmıyor" yüzündendi. BigJoy'un ürün
// sayfası besin değerlerini eksiksiz yayınlıyor ama hepsi div satırlarında —
// tablo arayan çıkarıcı hiçbirini göremiyordu.
//
// Aşağıdaki HTML, bigjoy.com.tr'deki gerçek bir ürün sayfasından
// (beef-and-whey-cikolata-2176g, 2026-09-05) alınmış parçadır. Snapshot
// olmasının sebebi: yapı değişirse test kırılsın, canlıda sessizce boş
// kalmasın.
public class BigJoyNutritionTests
{
    private const string GercekSayfaParcasi = """
        <html><body>
        <div class="ntin-dropdown2">
          <div class="nutrition-table row">
            <div class="nutrition-ustsatir col-lg-6">
              <div class="nutrition-title pt-1"> Son Kullanma Tarihi: <span>01/04/2029</span></div>
              <div class="nutrition-title"> Porsiyon Büyüklüğü: <span>32g</span></div>
              <div class="nutrition-title"> Porsiyon Sayısı: <span>68</span></div>
            </div>
            <div class="col-lg-6 scroll-bdeger">
              <div class="nutrition-secondary-title">Her Porsiyon İçin Miktar</div>
              <div>
                <div class="row bdegersatir m-0 dark-row">
                  <div class="col-8 satirsol">Enerji/Energy</div>
                  <div class="col-4 satirsag text-end">534kJ/126kcal</div>
                </div>
                <div class="row bdegersatir m-0 light-row">
                  <div class="col-8 satirsol">Yağ/Fat</div>
                  <div class="col-4 satirsag text-end">2,4g</div>
                </div>
                <div class="row bdegersatir m-0 dark-row">
                  <div class="col-8 satirsol">-Doymuş Yağ/Saturated Fat</div>
                  <div class="col-4 satirsag text-end">0,9g</div>
                </div>
                <div class="row bdegersatir m-0 light-row">
                  <div class="col-8 satirsol">Karbonhidrat/Carbohydrate</div>
                  <div class="col-4 satirsag text-end">2,2g</div>
                </div>
                <div class="row bdegersatir m-0 dark-row">
                  <div class="col-8 satirsol">-Şekerler/Sugars</div>
                  <div class="col-4 satirsag text-end">1,2g</div>
                </div>
                <div class="row bdegersatir m-0 light-row">
                  <div class="col-8 satirsol">Protein</div>
                  <div class="col-4 satirsag text-end">24g</div>
                </div>
              </div>
            </div>
          </div>
        </div>
        </body></html>
        """;

    private static BigJoyScraper Scraper(string html)
    {
        var client = new HttpClient(new SabitYanitHandler(html))
        {
            BaseAddress = new Uri("https://www.bigjoy.com.tr"),
        };
        return new BigJoyScraper(client);
    }

    [Fact]
    public async Task DivSatirlarindanBesinTablosuOkunuyor()
    {
        var details = await Scraper(GercekSayfaParcasi)
            .FetchDetailsAsync("https://www.bigjoy.com.tr/beef-and-whey-cikolata-2176g");

        Assert.NotNull(details.NutritionJson);
        var tablo = JsonSerializer.Deserialize<Dictionary<string, string>>(details.NutritionJson!)!;

        Assert.Equal("534kJ/126kcal", tablo["Enerji/Energy"]);
        Assert.Equal("2,4g", tablo["Yağ/Fat"]);
        Assert.Equal("2,2g", tablo["Karbonhidrat/Carbohydrate"]);
        Assert.Equal("24g", tablo["Protein"]);
    }

    // Servis başı protein, "servis başı maliyet" hesabının girdisi — JSON
    // içinden okunamadığı için ayrı kolonda tutuluyor.
    [Fact]
    public async Task ProteinGramiAyrıAlanaCikariliyor()
    {
        var details = await Scraper(GercekSayfaParcasi)
            .FetchDetailsAsync("https://www.bigjoy.com.tr/beef-and-whey-cikolata-2176g");

        Assert.Equal(24m, details.ProteinPerServingGrams);
    }

    // Asıl kazanç burada: BigJoy porsiyon büyüklüğünü ve paketteki servis
    // sayısını DOĞRUDAN beyan ediyor. İkisi de gelince servis başı fiyat
    // hesabı bu markada açılıyor — açıklama metninden çıkarıma gerek kalmıyor.
    [Fact]
    public async Task PorsiyonBuyuklugu_ve_ServisSayisi_KaynaginBeyanindanOkunuyor()
    {
        var details = await Scraper(GercekSayfaParcasi)
            .FetchDetailsAsync("https://www.bigjoy.com.tr/beef-and-whey-cikolata-2176g");

        Assert.Equal(32m, details.ServingSizeGrams);
        Assert.Equal(68, details.ServingsPerPackage);
    }

    // "Son Kullanma Tarihi: 01/04/2029" de aynı div sınıfında duruyor.
    // Gramaj regex'i oradaki sayıları yakalamamalı — yakalasaydı porsiyon
    // 1 gram sanılırdı ve servis başı fiyat 2176 kat şişerdi.
    [Fact]
    public async Task SonKullanmaTarihiPorsiyonSanilmiyor()
    {
        const string sadeceTarih = """
            <html><body>
            <div class="nutrition-title"> Son Kullanma Tarihi: <span>01/04/2029</span></div>
            </body></html>
            """;

        var details = await Scraper(sadeceTarih).FetchDetailsAsync("https://www.bigjoy.com.tr/x");

        Assert.Null(details.ServingSizeGrams);
        Assert.Null(details.ServingsPerPackage);
    }

    // Besin bölümü olmayan ürünlerde (aksesuar, shaker) UYDURMA veri
    // üretilmemeli — tablo yoksa null. Detay tamamlama servisi bu durumda
    // yalnızca "bakıldı" damgası atıyor.
    [Fact]
    public async Task BesinBolumuYoksaNullDonuyor()
    {
        const string besinsiz = "<html><body><div class='urun'><p>Shaker</p></div></body></html>";

        var details = await Scraper(besinsiz).FetchDetailsAsync("https://www.bigjoy.com.tr/shaker");

        Assert.Null(details.NutritionJson);
        Assert.Null(details.ProteinPerServingGrams);
    }

    // Açıklama BİLİNÇLİ olarak çekilmiyor: BigJoy'un açıklaması zaten normal
    // taramada (kategori ucundan) geliyor. Burada da okunsaydı aynı veri iki
    // farklı biçimde üretilir ve hangisinin kazandığı taramanın sırasına
    // bağlı kalırdı.
    [Fact]
    public async Task AciklamaCekilmiyor()
    {
        var details = await Scraper(GercekSayfaParcasi).FetchDetailsAsync("https://www.bigjoy.com.tr/x");

        Assert.Null(details.Description);
    }

    private sealed class SabitYanitHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html),
            });
    }
}

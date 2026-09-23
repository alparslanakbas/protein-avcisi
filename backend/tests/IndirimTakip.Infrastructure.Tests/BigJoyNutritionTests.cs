using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.BigJoy;

namespace IndirimTakip.Infrastructure.Tests;

// Bu testlerin varlık sebebi bir ÖLÇÜM (5 Eylül): katalogda besin değeri
// olan ürün 4.918'de 328'di (%6,7) ve eksiklerin bir kısmı "kaynakta veri
// yok" değil, "kaynak okunabilir bir tablo kullanmıyor" yüzündendi.
//
// 22 EYLÜL'DE SİTE YENİDEN YAZILDI ve bu testler kırıldı — işlerini yaptılar.
// Eski kalıp (div.bdegersatir, div.nutrition-title) sayfada artık SIFIR kez
// geçiyor; aşağıdaki HTML yeni yapının gerçek bir ürün sayfasından
// (creatine-255g, 2026-09-22) alınmış parçasıdır. Snapshot olmasının sebebi
// aynı: yapı yine değişirse test kırılsın, canlıda sessizce boş kalmasın.
//
// PORSİYON BLOĞU BESİN TABLOSUNUN DIŞINDA ve bu ölçülerek böyle kuruldu:
// canlı sayfada başlığın kapsayıcısı yalnızca besin satırlarını içeriyor
// (9 satır), porsiyon satırları ayrı bir kutuda. Kalıp bunu taklit etmezse
// test, kodun ayırt etmediği bir hatayı yakalayamaz.
public class BigJoyNutritionTests
{
    private const string GercekSayfaParcasi = """
        <html><body>
        <div class="py-6">
          <div class="border border-[#f0eee5] rounded-xl p-4 md:p-5">
            <div class="flex items-end justify-between mb-2">
              <h3 class="text-[18px] font-semibold">Besin Değerleri</h3>
              <span class="text-[14px] font-semibold text-gray-500">Her Porsiyon / 32g</span>
            </div>
            <div class="h-[2px] bg-gray-900 mb-2"></div>
            <div class="max-h-[360px] overflow-y-auto pr-1">
              <div class="py-2.5 border-b text-[13px]">
                <div class="flex justify-between items-start gap-3">
                  <span class="text-gray-600">Enerji/Energy</span><span class="font-semibold">534kJ/126kcal</span>
                </div>
              </div>
              <div class="py-2.5 border-b text-[13px]">
                <div class="flex justify-between items-start gap-3">
                  <span class="text-gray-600">Yağ/Fat</span><span class="font-semibold">2,4g</span>
                </div>
              </div>
              <div class="py-2.5 border-b text-[13px]">
                <div class="flex justify-between items-start gap-3">
                  <span class="text-gray-600">-Doymuş Yağ/Saturated Fat</span><span class="font-semibold">0,9g</span>
                </div>
              </div>
              <div class="py-2.5 border-b text-[13px]">
                <div class="flex justify-between items-start gap-3">
                  <span class="text-gray-600">Karbonhidrat/Carbohydrate</span><span class="font-semibold">2,2g</span>
                </div>
              </div>
              <div class="py-2.5 border-b text-[13px]">
                <div class="flex justify-between items-start gap-3">
                  <span class="text-gray-600">-Şekerler/Sugars</span><span class="font-semibold">1,2g</span>
                </div>
              </div>
              <div class="py-2.5 text-[13px]">
                <div class="flex justify-between items-start gap-3">
                  <span class="text-gray-600">Protein</span><span class="font-semibold">24g</span>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div style="display:none;">
          <div class="text-[14px] bg-white border border-gray-200 rounded-xl p-4">
            <div class="flex justify-between items-baseline py-2 border-b border-gray-100">
              <span class="text-gray-500">Son Kullanma Tarihi:</span><span class="font-bold">01/04/2029</span>
            </div>
            <div class="flex justify-between items-baseline py-2 border-b border-gray-100">
              <span class="text-gray-500">Porsiyon Büyüklüğü:</span><span class="font-bold">32g</span>
            </div>
            <div class="flex justify-between items-baseline py-2">
              <span class="text-gray-500">Porsiyon Sayısı:</span><span class="font-bold">68</span>
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
            <div class="flex justify-between items-baseline py-2">
              <span class="text-gray-500">Son Kullanma Tarihi:</span><span class="font-bold">01/04/2029</span>
            </div>
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

using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.Gnc;

namespace IndirimTakip.Infrastructure.Tests;

// GNC'nin kataloğu iki farklı şekil taşıyor ve ikisi de gerçek sayfalardan
// (gnc.com.tr, 2026-09-05) alınmıştır:
//   - protein tozunda "Besin Değerleri" alanı, klasik makro tablosu
//   - vitamin/kapsülde "İçindekiler" alanı, porsiyon başına ETKEN MADDE
//     tablosu ("Etken Madde | 1 Yumuşak Kapsüldeki Miktar")
// İkincisi bir içindekiler METNİ değil, gerçek bir tablo — vitamin ürününde
// "besin değeri"nin karşılığı bu.
public class GncNutritionTests
{
    private const string ProteinTozu = """
        <html><body><script id="__NEXT_DATA__" type="application/json">
        {"props":{"pageProps":{"pageSpecificData":{"attributes":[
          {"productAttribute":{"name":"Besin Değerleri","type":"HTML"},
           "value":"<table><tr><td>Besin</td><td>31,01 g</td></tr><tr><td>Enerji (kj / kcal)</td><td>496 kj / 120 kcal</td></tr><tr><td>Protein (g)</td><td>25 g</td></tr><tr><td>Karbonhidrat (g)</td><td>2 g</td></tr></table>"},
          {"productAttribute":{"name":"İçindekiler","type":"HTML"},"value":"<p>Peynir altı suyu proteini</p>"}
        ]}}}}
        </script></body></html>
        """;

    private const string Kapsul = """
        <html><body><script id="__NEXT_DATA__" type="application/json">
        {"props":{"pageProps":{"pageSpecificData":{"attributes":[
          {"productAttribute":{"name":"Kullanım Önerisi","type":"HTML"},"value":"<p>Günde 1 kapsül</p>"},
          {"productAttribute":{"name":"İçindekiler","type":"HTML"},
           "value":"<table><tr><td>Etken Madde</td><td>1 Yumuşak Kapsüldeki Miktar</td></tr><tr><td>Koenzim Q10</td><td>100 mg</td></tr></table>"}
        ]}}}}
        </script></body></html>
        """;

    private static GncScraper Scraper(string html) =>
        new(new HttpClient(new SabitYanitHandler(html)) { BaseAddress = new Uri("https://gnc.com.tr") });

    // "Besin" alanı VARSA o kullanılmalı — "İçindekiler" alanı da var ve o
    // düz metin; sıra ters olsaydı makro tablosu kaçırılırdı.
    [Fact]
    public async Task ProteinTozunda_BesinAlaniOnceligeSahip()
    {
        var d = await Scraper(ProteinTozu).FetchDetailsAsync("https://gnc.com.tr/whey");

        var tablo = JsonSerializer.Deserialize<Dictionary<string, string>>(d.NutritionJson!)!;
        Assert.Equal("25 g", tablo["Protein (g)"]);
        Assert.Equal(25m, d.ProteinPerServingGrams);
        // Porsiyon, tablo başlığındaki gramajdan geliyor.
        Assert.Equal(31.01m, d.ServingSizeGrams);
        // Başlık satırı veri sanılmamalı.
        Assert.DoesNotContain("Besin", tablo.Keys);
    }

    [Fact]
    public async Task KapsuldeEtkenMaddeTablosuOkunuyor()
    {
        var d = await Scraper(Kapsul).FetchDetailsAsync("https://gnc.com.tr/coq10");

        var tablo = JsonSerializer.Deserialize<Dictionary<string, string>>(d.NutritionJson!)!;
        Assert.Equal("100 mg", tablo["Koenzim Q10"]);
    }

    // Başlık "1 Yumuşak Kapsüldeki Miktar" — gram YOK. Kapsül sayısından
    // gramaj uydurmak yasak, alan boş kalmalı.
    [Fact]
    public async Task KapsuldePorsiyonGramiUydurulmuyor()
    {
        var d = await Scraper(Kapsul).FetchDetailsAsync("https://gnc.com.tr/coq10");

        Assert.Null(d.ServingSizeGrams);
        // Etken madde mg cinsinden — protein sanılmamalı.
        Assert.Null(d.ProteinPerServingGrams);
    }

    // Aynı adlı alan başka kaynakta DÜZ METİN olabiliyor (ProteinOcean'da
    // içindekiler listesi). Tablo yoksa uydurma üretilmiyor.
    [Fact]
    public async Task TabloYerineDuzMetinVarsaNullDonuyor()
    {
        const string metin = """
            <html><body><script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"pageSpecificData":{"attributes":[
              {"productAttribute":{"name":"İçindekiler","type":"HTML"},"value":"<p>Nohut unu, mısır unu, ayçiçek yağı.</p>"}
            ]}}}}
            </script></body></html>
            """;

        var d = await Scraper(metin).FetchDetailsAsync("https://gnc.com.tr/x");

        Assert.Null(d.NutritionJson);
        Assert.Null(d.ServingSizeGrams);
    }

    [Fact]
    public async Task AlanYoksaNullDonuyor()
    {
        var d = await Scraper("<html><body><p>yok</p></body></html>").FetchDetailsAsync("https://gnc.com.tr/y");

        Assert.Null(d.NutritionJson);
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

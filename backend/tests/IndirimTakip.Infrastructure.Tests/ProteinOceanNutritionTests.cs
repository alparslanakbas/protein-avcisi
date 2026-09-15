using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Tests;

// ProteinOcean (ikas) besin tablosu TABLE alanında: değer {colId,rowId,value}
// listesi (JSON DİZESİ olarak), satır/sütun adları productAttribute.tableTemplate
// içinde. Şekiller gerçek sayfalardan (2026-09-15) alındı: whey-isolate,
// pea-protein-lansman-1, protein-meal, tudca-1-, greens-superfoods-lansman-1.
public class ProteinOceanNutritionTests
{
    private sealed record Hucre(string Row, string Col, string Value);

    private static object Tablo(string ad, string[] satirlar, string[] sutunlar, params Hucre[] hucreler) => new
    {
        productAttribute = new
        {
            name = ad,
            type = "TABLE",
            tableTemplate = new
            {
                // Şablon bütün ürünlerde ortak: kullanılmayan sütunlar da duruyor.
                columns = sutunlar.Select(s => new { id = "c-" + s, name = s }),
                rows = satirlar.Select(s => new { id = "r-" + s, name = s }),
            },
        },
        value = JsonSerializer.Serialize(hucreler.Select(h => new { colId = "c-" + h.Col, rowId = "r-" + h.Row, value = h.Value })),
    };

    private static string Sayfa(params object[] alanlar)
    {
        var veri = new { props = new { pageProps = new { pageSpecificData = new { attributes = alanlar } } } };
        return "<html><body><script id=\"__NEXT_DATA__\" type=\"application/json\">"
            + JsonSerializer.Serialize(veri) + "</script></body></html>";
    }

    private static readonly string[] OrtakSutunlar = ["100 g", "3,5 g", "25 g servis için", "60 ML", "50 g servis için"];
    private static readonly string[] MakroSatirlar = ["Enerji", "Protein", "Karbonhidrat", "Yağ", "Tuz"];

    private static Dictionary<string, string> Oku(string? json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json!)!;

    private static Task<IndirimTakip.Core.Scraping.ProductDetails> Getir(string html) =>
        new ProteinOceanScraper(new HttpClient(new SabitYanitHandler(html)))
            .FetchDetailsAsync("https://proteinocean.com/urun");

    [Fact]
    public async Task PorsiyonSutunuOkunuyor()
    {
        var html = Sayfa(Tablo("BESİN DEĞERLERİ", MakroSatirlar, OrtakSutunlar,
            new Hucre("Enerji", "25 g servis için", "387 kJ/91 kcal"),
            new Hucre("Protein", "25 g servis için", "21.3 g"),
            new Hucre("Yağ", "25 g servis için", "0.3 g")));

        var d = await Getir(html);

        Assert.Equal("21.3 g", Oku(d.NutritionJson)["Protein"]);
        Assert.Equal(21.3m, d.ProteinPerServingGrams);
        Assert.Equal(25m, d.ServingSizeGrams);
    }

    // Pea Protein: yalnızca "100 g" sütunu. Sitede tablo "porsiyon başına"
    // sunulduğu için 81 g porsiyon başı protein diye YAZILMAMALI.
    [Fact]
    public async Task YalnizcaYuzGramSutunuVarsaTabloBosKaliyor()
    {
        var html = Sayfa(Tablo("BESİN DEĞERLERİ", MakroSatirlar, OrtakSutunlar,
            new Hucre("Protein", "100 g", "81 g"),
            new Hucre("Enerji", "100 g", "1600 kJ/380 kcal")));

        var d = await Getir(html);

        Assert.Null(d.NutritionJson);
        Assert.Null(d.ProteinPerServingGrams);
        Assert.Null(d.ServingSizeGrams);
    }

    // Enerji jeli: "60 ML" gram vermiyor, porsiyon olduğu anlaşılmıyor.
    [Fact]
    public async Task MililitreSutunuPorsiyonSayilmiyor()
    {
        var html = Sayfa(Tablo("BESİN DEĞERLERİ", MakroSatirlar, OrtakSutunlar,
            new Hucre("Karbonhidrat", "60 ML", "25 g")));

        Assert.Null((await Getir(html)).NutritionJson);
    }

    // Protein Meal: ayrı adlı tablo, iki sütun dolu; 100 g değil 60 g okunmalı.
    [Fact]
    public async Task EnerjiVeBesinOgeleriTablosundaPorsiyonSeciliyor()
    {
        var html = Sayfa(Tablo("ENERJİ VE BESİN ÖĞELERİ", MakroSatirlar, ["100g", "60g (1 servis)"],
            new Hucre("Protein", "100g", "41 g"),
            new Hucre("Protein", "60g (1 servis)", "25 g"),
            new Hucre("Karbonhidrat", "100g", "35 g"),
            new Hucre("Karbonhidrat", "60g (1 servis)", "21 g")));

        var d = await Getir(html);

        Assert.Equal(25m, d.ProteinPerServingGrams);
        Assert.Equal(60m, d.ServingSizeGrams);
        Assert.Equal("21 g", Oku(d.NutritionJson)["Karbonhidrat"]);
    }

    // TUDCA: makro tablo yok, etken madde tablosu var (GNC/Kiperin emsali).
    [Fact]
    public async Task MakroTabloYoksaEtkenMaddeTablosuOkunuyor()
    {
        var html = Sayfa(Tablo("BİLEŞEN ADI", ["Magnezyum", "Tauroursodeoksikolik Asit"],
            ["1 tablette", "1 serviste (2 tablet)", "1 kapsülde"],
            new Hucre("Tauroursodeoksikolik Asit", "1 kapsülde", "250 mg"),
            new Hucre("Magnezyum", "1 tablette", "")));

        var d = await Getir(html);

        Assert.Equal("250 mg", Oku(d.NutritionJson)["Tauroursodeoksikolik Asit"]);
        Assert.Single(Oku(d.NutritionJson));
        Assert.Null(d.ServingSizeGrams);
        Assert.Null(d.ProteinPerServingGrams);
    }

    // Greens: makro tablo yalnızca 100 g (okunmaz), etken madde tablosu devreye girer.
    [Fact]
    public async Task YuzGramlikMakroTabloEtkenMaddeTablosunuEngellemiyor()
    {
        var html = Sayfa(
            Tablo("BESİN DEĞERLERİ", MakroSatirlar, OrtakSutunlar, new Hucre("Protein", "100 g", "11 g")),
            Tablo("BİLEŞEN ADI", ["İnulin", "Spirulina"], ["6 g", "1 kapsülde"],
                new Hucre("İnulin", "6 g", "2715 mg"),
                new Hucre("Spirulina", "6 g", "500 mg")));

        var d = await Getir(html);

        Assert.Equal("500 mg", Oku(d.NutritionJson)["Spirulina"]);
        Assert.DoesNotContain("Protein", Oku(d.NutritionJson).Keys);
    }

    // İki porsiyon sütunu dolu ve ikisi de "servis" demiyorsa sütun tahmin edilmiyor.
    [Fact]
    public async Task BelirsizSutundaTabloOkunmuyor()
    {
        var html = Sayfa(Tablo("BİLEŞEN ADI", ["Magnezyum"], ["1 tablette", "1 kapsülde"],
            new Hucre("Magnezyum", "1 tablette", "100 mg"),
            new Hucre("Magnezyum", "1 kapsülde", "50 mg")));

        Assert.Null((await Getir(html)).NutritionJson);
    }

    [Fact]
    public async Task BirdenFazlaSutundaServisDiyenSeciliyor()
    {
        var html = Sayfa(Tablo("BİLEŞEN ADI", ["Magnezyum"], ["1 kapsülde", "1 serviste (2 kapsül)"],
            new Hucre("Magnezyum", "1 kapsülde", "100 mg"),
            new Hucre("Magnezyum", "1 serviste (2 kapsül)", "200 mg")));

        Assert.Equal("200 mg", Oku((await Getir(html)).NutritionJson)["Magnezyum"]);
    }

    // Şablonda karşılığı olmayan satır kimliği: değer yanlış satıra yazılmamalı.
    [Fact]
    public async Task SablondaOlmayanSatirAtlaniyor()
    {
        var tablo = Tablo("BESİN DEĞERLERİ", MakroSatirlar, OrtakSutunlar,
            new Hucre("Protein", "25 g servis için", "22 g"),
            new Hucre("Bilinmeyen", "25 g servis için", "99 g"));

        var d = await Getir(Sayfa(tablo));

        Assert.Single(Oku(d.NutritionJson));
        Assert.Equal(22m, d.ProteinPerServingGrams);
    }

    [Fact]
    public async Task TabloYoksaAciklamaYineGeliyor()
    {
        var html = Sayfa(new
        {
            productAttribute = new { name = "2- ÜRÜN SAYFA - ÖZELLİKLER", type = "HTML" },
            value = "<p>Yüksek proteinli.</p>",
        });

        var d = await Getir(html);

        Assert.Null(d.NutritionJson);
        Assert.Contains("Yüksek proteinli", d.Description);
    }

    private sealed class SabitYanitHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }
}

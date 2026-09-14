using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.Kiperin;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndirimTakip.Infrastructure.Tests;

// Kiperin (kiperinturkiye.com, ikas) tabloyu özellik alanına değil ürün
// AÇIKLAMASININ içine koyuyor. Yapı gerçek sayfalardan (2026-09-15) alındı:
//   - kapsül/vitamin: "Etken Madde | Miktar | %BRD" (GNC ile aynı karar:
//     vitamin ürününde besin değerinin karşılığı porsiyon başına etken madde)
//   - kolajen tozu: "Bileşen | Miktar" içinde Enerji/Protein/Karbonhidrat/Yağ
//   - kolajen sayfasında translations[0].description'da İNGİLİZCE kopya da var
public class KiperinNutritionTests
{
    // Düz birleştirme, bilerek: JSON'un kapanışındaki "}}}}" ham
    // interpolasyonlu dizede derleyici hatası veriyordu (CS9007).
    private static string Sayfa(string aciklamaHtml, string? ingilizceAciklama = null)
    {
        var ceviri = ingilizceAciklama is null
            ? ""
            : ",\"translations\":[{\"locale\":\"en\",\"description\":" + JsonSerializer.Serialize(ingilizceAciklama) + "}]";

        return "<html><body><script id=\"__NEXT_DATA__\" type=\"application/json\">"
            + "{\"props\":{\"pageProps\":{\"pageSpecificData\":{"
            + "\"attributes\":[{\"productAttribute\":{\"name\":\"Marka\",\"type\":\"TEXT\"},\"value\":\"Kiperin\"}],"
            + "\"description\":" + JsonSerializer.Serialize(aciklamaHtml)
            + ceviri
            + "}}}}</script></body></html>";
    }

    private const string AlphaMan = """
        <h2>Kiperin Alpha Man Nedir?</h2><p>Bitki ekstreleri.</p>
        <table><tr><th>Etken Madde</th><th>Miktar</th><th>%BRD</th></tr>
        <tr><td>Çoban Çökerten (Tribulus) Ekstresi</td><td>400 mg</td><td>-</td></tr>
        <tr><td>Magnezyum</td><td>250 mg</td><td>-</td></tr></table>
        """;

    private const string ClassicCollagen = """
        <h2>Kiperin Classic Collagen Nedir?</h2>
        <table><tr><td>Bileşen</td><td>Miktar</td></tr>
        <tr><td>Enerji</td><td>36 kcal</td></tr><tr><td>Protein</td><td>9 g</td></tr>
        <tr><td>Kolajen Peptitleri</td><td>10 g</td></tr><tr><td>Karbonhidrat</td><td>0 g</td></tr>
        <tr><td>Toplam Yağ</td><td>0 g</td></tr></table>
        """;

    private static KiperinScraper Scraper(string html) =>
        new(new HttpClient(new SabitYanitHandler(html)), NullLogger<KiperinScraper>.Instance);

    private static Dictionary<string, string> Tablo(string? json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json!)!;

    // "%BRD" en sağdaki sütun ama yüzde; "Miktar" okunmalı, "-" değil.
    [Fact]
    public async Task EtkenMaddeTablosundaMiktarSutunuOkunuyor()
    {
        var d = await Scraper(Sayfa(AlphaMan)).FetchDetailsAsync("https://kiperinturkiye.com/alpha-man-120-kapsul");

        var tablo = Tablo(d.NutritionJson);
        Assert.Equal("400 mg", tablo["Çoban Çökerten (Tribulus) Ekstresi"]);
        Assert.Equal("250 mg", tablo["Magnezyum"]);
        Assert.DoesNotContain("Etken Madde", tablo.Keys);
        // mg cinsinden etken madde protein sanılmamalı; başlık gram vermiyor.
        Assert.Null(d.ProteinPerServingGrams);
        Assert.Null(d.ServingSizeGrams);
    }

    [Fact]
    public async Task KolajenTozundaProteinOkunuyor()
    {
        var d = await Scraper(Sayfa(ClassicCollagen)).FetchDetailsAsync("https://kiperinturkiye.com/kiperin-classic-collagen");

        Assert.Equal("36 kcal", Tablo(d.NutritionJson)["Enerji"]);
        Assert.Equal(9m, d.ProteinPerServingGrams);
    }

    // İngilizce kopya farklı değerle verilirse Türkçe açıklama kazanmalı.
    [Fact]
    public async Task IngilizceCeviriTablosuOkunmuyor()
    {
        const string ingilizce = """
            <table><tr><td>Ingredient</td><td>Amount</td></tr><tr><td>Energy</td><td>99 kcal</td></tr><tr><td>Protein</td><td>77 g</td></tr></table>
            """;

        var d = await Scraper(Sayfa(ClassicCollagen, ingilizce)).FetchDetailsAsync("https://kiperinturkiye.com/kiperin-classic-collagen");

        var tablo = Tablo(d.NutritionJson);
        Assert.DoesNotContain("Energy", tablo.Keys);
        Assert.Equal(9m, d.ProteinPerServingGrams);
    }

    // Canlıda TUDCA, Urolithin A, Omega 3 gibi tabloların son satırı kutudaki
    // adetti; besin tablosuna girmemeli, etken madde satırı kalmalı.
    [Theory]
    [InlineData("Kapsül Sayısı")]
    [InlineData("KAPSÜL SAYISI")]
    [InlineData("Softgel Sayısı")]
    public async Task KutudakiAdetSatiriTabloyaGirmiyor(string etiket)
    {
        var tudca = $"""
            <table><tr><td>Etken Madde</td><td>Miktar</td></tr>
            <tr><td>TUDCA (Tauroursodeoksikolik Asit)</td><td>250 mg</td></tr>
            <tr><td>{etiket}</td><td>30</td></tr></table>
            """;

        var d = await Scraper(Sayfa(tudca)).FetchDetailsAsync("https://kiperinturkiye.com/tudca-250mg-30-kapsul");

        var tablo = Tablo(d.NutritionJson);
        Assert.Equal("250 mg", tablo["TUDCA (Tauroursodeoksikolik Asit)"]);
        Assert.DoesNotContain(etiket, tablo.Keys);
        Assert.Single(tablo);
    }

    [Fact]
    public async Task TabloOlmayanAciklamadaNullDonuyor()
    {
        var d = await Scraper(Sayfa("<p>Yüz serumu, günde iki kez uygulanır.</p>")).FetchDetailsAsync("https://kiperinturkiye.com/serum");

        Assert.Null(d.NutritionJson);
        Assert.Null(d.ProteinPerServingGrams);
    }

    [Fact]
    public async Task SayfaVerisiYoksaNullDonuyor()
    {
        var d = await Scraper("<html><body><p>yok</p></body></html>").FetchDetailsAsync("https://kiperinturkiye.com/y");

        Assert.Null(d.NutritionJson);
        Assert.Null(d.Description);
    }

    private sealed class SabitYanitHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }
}

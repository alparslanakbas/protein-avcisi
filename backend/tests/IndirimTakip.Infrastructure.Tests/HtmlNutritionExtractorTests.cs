using System.Text.Json;
using HtmlAgilityPack;
using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Bu testler gerçek ürün sayfalarından (2026-08-21'de takehiq.com,
// hardlinenutrition.com, ssnsports.com.tr'den) alınan ham HTML parçalarını
// kullanıyor — üç markanın yapısı da başlangıçta varsayılandan farklı çıktı
// (HIQ: 3 kolonlu, son kolon porsiyon başına; Hardline: her satır ayrı
// div.satirlar; SSN: hiç <table> yok, <strong>etiket</strong> — değer<br>
// deseni). Bu snapshot'lar bir daha aynı hatanın (yanlış kolonu/yanlış
// yapıyı okuma) fark edilmeden geri gelmesini engelliyor.
public class HtmlNutritionExtractorTests
{
    [Fact]
    public void HiqTablosu_SonKolonuPorsiyonBasinaDegerOlarakAlir()
    {
        // Gerçek HIQ CREA500 ürününden — "Bileşen | 100 g | 4 g" başlıklı,
        // son kolon (4 g porsiyon) gerçek servis başı değer.
        const string html = """
            <table class="nutrition-table">
            <tbody>
            <tr><th>Bileşen</th><th>100 g</th><th>4 g</th></tr>
            <tr><td>Enerji</td><td>102 kJ / 24 kcal</td><td>8 kJ / 1 kcal</td></tr>
            <tr><td>Protein</td><td>0 g</td><td>0 g</td></tr>
            <tr><td>Kreatin Monohidrat 500 Mesh (CREA500®)</td><td>75 g</td><td>3 g</td></tr>
            </tbody>
            </table>
            """;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var table = doc.DocumentNode.SelectSingleNode("//table");
        var json = NutritionParser.BuildNutritionJson(HtmlNutritionExtractor.FromTables(table));
        var parsed = Deserialize(json);

        // 100 g'lık ("75 g") değil, gerçek porsiyon ("3 g") değeri yakalanmalı.
        Assert.Equal("3 g", parsed["Kreatin Monohidrat 500 Mesh (CREA500®)"]);
        // Başlık satırı ("Bileşen") veri satırı olarak sızmamalı.
        Assert.False(parsed.ContainsKey("Bileşen"));
    }

    [Fact]
    public void HardlineSatirlari_HerBiriAyriDivOlarakOkunur()
    {
        // Gerçek Hardline "Kreatin % Mikronize 300 Gr" ürününden.
        const string html = """
            <div class="satirlar"><span class="baslik">Enerji/Calorie</span> <span class="deger">0 kcal (0 kj)</span></div>
            <div class="satirlar"><span class="baslik">Protein/Protein</span> <span class="deger">0 g</span></div>
            <div class="satirlar"><span class="baslik">Kreatin Monohidrat/Creatine Monohydrate</span> <span class="deger">5 g</span></div>
            """;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode
            .SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' satirlar ')]")!
            .Select(row =>
            {
                var label = row.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' baslik ')]");
                var value = row.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' deger ')]");
                return (Label: label!.InnerText.Trim(), Value: value!.InnerText.Trim());
            });

        var json = NutritionParser.BuildNutritionJson(rows);
        var parsed = Deserialize(json);

        // "Protein/Protein" gibi TAM aynı tekrar sadeleşiyor ama "Kreatin
        // Monohidrat/Creatine Monohydrate" gerçek bir çeviri (aynı değil),
        // sadeleştirilmemeli — NormalizeLabel bilinçli olarak sadece
        // birebir aynı iki yarıyı birleştiriyor.
        Assert.Equal("5 g", parsed["Kreatin Monohidrat/Creatine Monohydrate"]);
        Assert.Equal(22m, NutritionParser.ExtractProteinGrams(
            NutritionParser.BuildNutritionJson([("Protein/Protein", "22 g")])));
    }

    [Fact]
    public void SsnDesenlerini_TabloOlmadanOkur()
    {
        // Gerçek SSN "Raw and Natural Creatine" ürününden — hiç <table> yok,
        // <strong>etiket</strong> — değer<br> art arda.
        const string html = """
            <p><strong>Enerji</strong> — 0 kj / 0 kcal<br>
            <strong>Protein</strong> — 0 g<br>
            <strong>— Şekerler</strong> — 0 g<br>
            <strong>Kreatin Monohidrat</strong> — 5 g</p>
            """;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var rows = HtmlNutritionExtractor.FromLabelDashValuePattern(doc.DocumentNode);
        var json = NutritionParser.BuildNutritionJson(rows);
        var parsed = Deserialize(json);

        Assert.Equal("5 g", parsed["Kreatin Monohidrat"]);
        // Alt kalemdeki öndeki tire hem etikette hem değerde temizlenmeli.
        Assert.Equal("0 g", parsed["Şekerler"]);
        Assert.False(parsed.ContainsKey("— Şekerler"));
    }

    private static Dictionary<string, string> Deserialize(string? json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json!)!;
}

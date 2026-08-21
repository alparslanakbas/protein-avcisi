using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

public class NutritionParserTests
{
    [Fact]
    public void BuildNutritionJson_AnlamliSatirYoksa_NullDoner()
    {
        Assert.Null(NutritionParser.BuildNutritionJson([]));
        // Sayı içermeyen satırlar tabloya alınmıyor (araya karışan metin).
        Assert.Null(NutritionParser.BuildNutritionJson([("Ürün Açıklaması", "Harika bir ürün")]));
    }

    [Fact]
    public void BuildNutritionJson_TekrarEdenEtiketiSadelestirir()
    {
        // Hardline "Protein / Protein" gibi Türkçe/İngilizce ikili etiket veriyor.
        var json = NutritionParser.BuildNutritionJson([("Protein / Protein", "22 g")]);

        Assert.NotNull(json);
        Assert.Contains("\"Protein\"", json);
        Assert.DoesNotContain("Protein / Protein", json);
    }

    [Fact]
    public void BuildNutritionJson_AyniEtiketTekrarlarsaIlkiniKorur()
    {
        var json = NutritionParser.BuildNutritionJson([("Protein", "24 g"), ("Protein", "48 %RDA")]);

        Assert.NotNull(json);
        Assert.Contains("24 g", json);
        Assert.DoesNotContain("48", json);
    }

    [Theory]
    [InlineData("24 g", 24)]
    [InlineData("24,5 g", 24.5)]
    [InlineData("23.8g", 23.8)]
    public void ExtractProteinGrams_FarkliYazimlariOkur(string value, decimal expected)
    {
        var json = NutritionParser.BuildNutritionJson([("Protein", value)]);

        Assert.Equal(expected, NutritionParser.ExtractProteinGrams(json));
    }

    [Fact]
    public void ExtractProteinGrams_GramDisiBirimleriAtlar()
    {
        // "Proteinden gelen kalori" gibi satırlar protein MİKTARI değil.
        var json = NutritionParser.BuildNutritionJson([("Proteinden gelen enerji", "96 kcal")]);

        Assert.Null(NutritionParser.ExtractProteinGrams(json));
    }

    [Fact]
    public void ExtractProteinGrams_UrunAdiSatirlariniAtlar()
    {
        // "Protein Tozu: 900 g" paket bilgisi, porsiyon başı protein değil.
        var json = NutritionParser.BuildNutritionJson([("Protein Tozu", "900 g")]);

        Assert.Null(NutritionParser.ExtractProteinGrams(json));
    }

    [Fact]
    public void ExtractProteinGrams_MakulAralikDisindakiDegeriReddeder()
    {
        // 100 g'ı aşan bir "porsiyon başı protein" yanlış satır yakalandığına işaret eder.
        var json = NutritionParser.BuildNutritionJson([("Protein", "900 g")]);

        Assert.Null(NutritionParser.ExtractProteinGrams(json));
    }

    [Fact]
    public void ExtractProteinGrams_VeriYoksaNullDoner()
    {
        Assert.Null(NutritionParser.ExtractProteinGrams(null));
        Assert.Null(NutritionParser.ExtractProteinGrams("bozuk json"));
        Assert.Null(NutritionParser.ExtractProteinGrams(
            NutritionParser.BuildNutritionJson([("Karbonhidrat", "3 g")])));
    }
}

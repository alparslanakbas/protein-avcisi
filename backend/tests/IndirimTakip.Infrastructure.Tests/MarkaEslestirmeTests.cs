using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Marka eşleştirmesinin harf/boşluk katmanı. Bu testler olmasaydı her yeni
/// bayi katalogda KOPYA marka üretirdi: marka önbelleği ordinal ve büyük/küçük
/// harf duyarlı, yani "TREC" ile "Trec" ayrı kabul ediliyordu.
/// </summary>
public class MarkaEslestirmeTests
{
    [Theory]
    // Salt büyük/küçük harf.
    [InlineData("TREC", "Trec")]
    [InlineData("CELLUCOR", "Cellucor")]
    [InlineData("ZOOMAD LABS", "Zoomad Labs")]
    [InlineData("JNX Sports", "Jnx Sports")]
    [InlineData("DY NUTRITION", "DY Nutrition")]
    [InlineData("ON THE GO", "On The Go")]
    // Türkçe NOKTALI İ — .NET'in kültürden bağımsız karşılaştırması bunu
    // "i" ile katlamıyor, yani bu satırların hiçbiri kendiliğinden eşleşmez.
    [InlineData("PRİME NUTRİTİON", "Prime Nutrition")]
    [InlineData("APPLİED NUTRİTİON", "Applied Nutrition")]
    [InlineData("EFFİVE NUTRİTİON", "Effive Nutrition")]
    [InlineData("KİNGSİZE", "Kingsize")]
    [InlineData("ENERVİT", "Enervit")]
    [InlineData("DYMATİZE", "Dymatize")]
    [InlineData("SİS", "SiS")]
    [InlineData("BİTE & MORE", "Bite & More")]
    // Boşluk ve nokta.
    [InlineData("MEAL JOY", "Mealjoy")]
    [InlineData("Dr. Pan", "Dr Pan")]
    [InlineData("Big Joy", "BigJoy")]
    public void AyniMarkaninFarkliYazimlariAyniKovayaDusuyor(string a, string b)
        => Assert.Equal(ScrapeIngestionService.FoldBrandName(a), ScrapeIngestionService.FoldBrandName(b));

    [Theory]
    // Farklı üreticiler birleşmemeli.
    [InlineData("Prime Nutrition", "Prime Hydration")]
    [InlineData("Z-Konzept", "Zkonzept")]   // tire bilerek atılmıyor
    [InlineData("Nuclear Nutrition", "Nuclear")]
    [InlineData("BigJoy", "Big Joy Sports")]
    public void FarkliAdlarAyriKaliyor(string a, string b)
        => Assert.NotEqual(ScrapeIngestionService.FoldBrandName(a), ScrapeIngestionService.FoldBrandName(b));

    [Fact]
    public void NoktasizIVeNoktaliIAyniKarakteregeIniyor()
    {
        // Türkçe'nin dört i harfi de tek bir kovaya inmeli: I ı İ i.
        Assert.Equal("iiii", ScrapeIngestionService.FoldBrandName("Iıİi"));
    }
}

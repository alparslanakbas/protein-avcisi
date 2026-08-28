using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Bu sınıfın çıktısı frontend'deki `core/slugify.ts` ile BİREBİR aynı olmak
// zorunda: IndexNow'a bildirdiğimiz adres kanonik adresle eşleşmezse bildirim
// yönlendirmeye düşer ve değerini kaybeder.
//
// Beklenen değerler canlı sitedeki gerçek adreslerden alındı.
public class SlugifierTests
{
    [Theory]
    // Canlı adreslerden birebir doğrulanmış örnekler
    [InlineData("HIQ ALPHA T-MAN 30 CAPS.", "hiq-alpha-t-man-30-caps")]
    [InlineData("Creatine Creapure® 500 Gr", "creatine-creapure-500-gr")]
    [InlineData("HIQ Vitargo Dual Force 1000g", "hiq-vitargo-dual-force-1000g")]
    [InlineData("Pre-Season Fırsatları-1", "pre-season-firsatlari-1")]
    [InlineData("HIQ Bcaa Nrg 390g", "hiq-bcaa-nrg-390g")]
    public void GercekUrunAdlariniKanonikAdreseCevirir(string name, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(name));
    }

    [Theory]
    // Türkçe harfler: bu projede üç kez hataya yol açtı (tr-TR ile büyük I
    // noktasız ı oluyor, ToLowerInvariant ile büyük İ hiç küçülmüyor).
    [InlineData("Çikolatalı Protein Bar", "cikolatali-protein-bar")]
    [InlineData("ÜZÜM AROMALI", "uzum-aromali")]
    [InlineData("İZOLE WHEY", "izole-whey")]
    [InlineData("Şeftali & Ğ Testi", "seftali-g-testi")]
    [InlineData("KREATİN MİKRONİZE", "kreatin-mikronize")]
    public void TurkceHarfleriDogruEsler(string name, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(name));
    }

    [Theory]
    [InlineData("  Baştaki ve sondaki boşluk  ", "bastaki-ve-sondaki-bosluk")]
    [InlineData("Çoklu   boşluk", "coklu-bosluk")]
    [InlineData("Noktalama!!! ??? ...", "noktalama")]
    [InlineData("100% Pure & Natural", "100-pure-natural")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NoktalamaVeBoslugiTemizler(string name, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(name));
    }

    [Fact]
    public void UzunAdlariKelimeOrtasindanKesmeden_KirpAr()
    {
        var uzun = "SSN Whey Refuel 1800g Çikolatalı Artı SSN Creatine 300g Artı SSN Glutamine 300g Kombinasyon Paketi";
        var slug = Slugifier.Slugify(uzun);

        Assert.True(slug.Length <= 80);
        // Kelime ortasından kesilmemeli: sonda yarım kelime kalmamalı.
        Assert.DoesNotContain("--", slug);
        Assert.False(slug.EndsWith('-'));
    }
}

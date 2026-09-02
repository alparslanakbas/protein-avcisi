using IndirimTakip.Infrastructure.Scraping.Gnc;

namespace IndirimTakip.Infrastructure.Tests;

// Örnek adlar GNC'nin gerçek kataloğundan alındı (1 Eylül 2026, ikas
// storefront API'si) — uydurulmadı.
public class GncScraperTests
{
    [Theory]
    [InlineData("Creatine MonoHydrate – 510 g (100 servis)", 100)]
    [InlineData("Creatine MonoHydrate – 255 g (50 servis)", 50)]
    [InlineData("Pro L-Glutamine – 905 g (181 servis)", 181)]
    [InlineData("GNC PRO – Whey Protein – 2201 g (64 servis) - Bisküvi", 64)]
    [InlineData("GNC Pro Bulk 1340 – 5443 g (15 servis)", 15)]
    public void ServisSayisiniUrunAdindanOkur(string ad, int beklenen)
    {
        Assert.Equal(beklenen, GncScraper.ExtractServingsPerPackage(ad));
    }

    // CLAUDE.md kuralı: marka bilgiyi vermiyorsa tahmin ÜRETİLMEZ, alan boş
    // kalır. GNC'nin vitamin tarafında servis sayısı adlarda hiç geçmiyor.
    [Theory]
    [InlineData("GNC CoQ-10 100 mg 30 Yumuşak Kapsül")]
    [InlineData("Vitamin D3 – 180 Tablet")]
    [InlineData("Herbal Plus Milk Thistle - 60 Tablet")]
    [InlineData("GNC 5 - HTP")]
    public void ServisSayisiYoksaBosBirakir(string ad)
    {
        Assert.Null(GncScraper.ExtractServingsPerPackage(ad));
    }

    // Gramaj ile servis sayısı aynı adda yan yana duruyor ("510 g (100
    // servis)"). Kalıp "servis" sözcüğüne bağlı olduğu için gramajı servis
    // sanmamalı — bu karışma olsaydı servis başı fiyat sessizce yanlış çıkardı.
    [Fact]
    public void GramajiServisSayisiSanmaz()
    {
        Assert.Equal(100, GncScraper.ExtractServingsPerPackage("Creatine MonoHydrate – 510 g (100 servis)"));
        Assert.Equal(33, GncScraper.ExtractServingsPerPackage("GNC AMP - Wheybolic – 1798,5 g (33 servis) - Çikolata"));
    }

    [Theory]
    [InlineData("1500 servis")]
    [InlineData("0 servis")]
    public void MakulOlmayanServisSayisiniAlmaz(string ad)
    {
        Assert.Null(GncScraper.ExtractServingsPerPackage(ad));
    }
}

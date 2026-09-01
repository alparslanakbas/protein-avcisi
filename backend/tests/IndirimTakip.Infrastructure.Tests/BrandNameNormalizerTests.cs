using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Marka adı normalizasyonu kozmetik değil: iki yazım aynı adrese çözülüyor
/// (brandSlug küçük harfe indiriyor), yani birleştirilmezse sitemap'e tekrar
/// eden adresler giriyor ve markalardan biri hiç açılamıyor.
/// </summary>
public class BrandNameNormalizerTests
{
    [Theory]
    [InlineData("Proteinocean")]
    [InlineData("Protein Ocean")]
    [InlineData("ProteinOcean")]
    public void AyniUreticininFarkliYazimlariTekAdaIner(string yazim)
    {
        Assert.Equal("ProteinOcean", BrandNameNormalizer.Normalize(yazim));
    }

    [Theory]
    [InlineData("Big Joy", "BigJoy")]
    [InlineData("Bigjoy", "BigJoy")]
    [InlineData("Swiss", "Swiss Nutrition")]
    [InlineData("Zero Shot", "ZeroShot")]
    [InlineData("Trec Nutrition", "Trec")]
    public void BilinenTakmaAdlarKanonigeCevrilir(string ham, string beklenen)
    {
        Assert.Equal(beklenen, BrandNameNormalizer.Normalize(ham));
    }

    [Fact]
    public void BosluklarKirpilir()
    {
        Assert.Equal("ProteinOcean", BrandNameNormalizer.Normalize("  Proteinocean  "));
    }

    [Theory]
    [InlineData("Olimp")]
    [InlineData("Mustang Nutrition")]
    [InlineData("Grenade")]
    public void BilinmeyenMarkaOlduguGibiKalir(string ad)
    {
        // Tahmin üretilmiyor: benzer isimli FARKLI üreticileri birleştirmek
        // uydurma veri olurdu.
        Assert.Equal(ad, BrandNameNormalizer.Normalize(ad));
    }
}

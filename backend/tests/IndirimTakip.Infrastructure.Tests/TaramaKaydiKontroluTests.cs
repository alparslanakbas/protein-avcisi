using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Her kaynağın geçtiği ortak kontrol (güvenlik/mimari incelemesi, 26 Eylül).
/// Kurallar scraper'lara tek tek yazıldığında kaymıştı: HIQ'da sıfır fiyat
/// koruması yoktu.
/// </summary>
public class TaramaKaydiKontroluTests
{
    private static ScrapedProduct Urun(
        decimal fiyat = 899m,
        string adres = "https://hiqnutrition.com/products/whey",
        string? gorsel = "https://cdn.shopify.com/a.jpg",
        string? kategori = "protein-tozu") =>
        new("HIQ Whey 2 kg", adres, gorsel, kategori, fiyat);

    [Fact]
    public void Gecerli_kayit_oldugu_gibi_geciyor()
    {
        var urun = Urun();

        Assert.Same(urun, TaramaKaydiKontrolu.Temizle(urun));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sifir_ya_da_negatif_fiyat_alinmiyor(decimal fiyat)
    {
        Assert.Null(TaramaKaydiKontrolu.Temizle(Urun(fiyat: fiyat)));
    }

    [Theory]
    [InlineData("http://hiqnutrition.com/products/whey")]
    [InlineData("/products/whey")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public void Https_olmayan_urun_adresi_alinmiyor(string adres)
    {
        Assert.Null(TaramaKaydiKontrolu.Temizle(Urun(adres: adres)));
    }

    [Theory]
    [InlineData("http://cdn.ornek.com/a.jpg")]
    [InlineData("//cdn.ornek.com/a.jpg")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Https_olmayan_gorsel_bosaliyor_urun_kaliyor(string gorsel)
    {
        var sonuc = TaramaKaydiKontrolu.Temizle(Urun(gorsel: gorsel));

        Assert.NotNull(sonuc);
        Assert.Null(sonuc.ImageUrl);
        Assert.Equal(899m, sonuc.Price);
    }

    /// <summary>
    /// Ham kaynak etiketi ("Protein Tozu", "Supplements") hiçbir kategori
    /// sayfasında görünmez; boşalınca isimden çıkarım devreye giriyor.
    /// </summary>
    [Theory]
    [InlineData("Protein Tozu")]
    [InlineData("Supplements")]
    [InlineData("")]
    public void Listede_olmayan_kategori_bosaliyor(string kategori)
    {
        var sonuc = TaramaKaydiKontrolu.Temizle(Urun(kategori: kategori));

        Assert.NotNull(sonuc);
        Assert.Null(sonuc.Category);
    }

    [Fact]
    public void Kategorisiz_ve_gorselsiz_kayit_geciyor()
    {
        var sonuc = TaramaKaydiKontrolu.Temizle(Urun(gorsel: null, kategori: null));

        Assert.NotNull(sonuc);
        Assert.Null(sonuc.ImageUrl);
        Assert.Null(sonuc.Category);
    }
}

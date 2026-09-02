using IndirimTakip.Infrastructure.Scraping.Vitabear;

namespace IndirimTakip.Infrastructure.Tests;

// Örnek kayıtlar vitabear.com.tr'nin /products/get?cat=all çıktısından
// birebir alındı (3 Eylül 2026).
public class VitabearScraperTests
{
    private const string BaseUrl = "https://www.vitabear.com.tr";

    private const string Katalog = """
        [
          {"id":15,"stock_code":"VTB12","name":"Vita Bear Hair Plus Vegan Saç Vitamini",
           "newPrice":"1.145,00 ₺","oldPrice":"","numericPrice":1144.9999999999998,
           "outOfStock":false,"slug":"hair-plus-vegan-sac-vitamini",
           "normalImg":"/media/uploads/images/Hair Plus Ayıcık 170965131855136c819.png",
           "desc_strip_tags":"Vita Bear Hair Plus, tamamen vegan içeriğiyle..."},
          {"id":21,"stock_code":"VTB30","name":"Vita Bear Sleepy Bear 2'li Paket",
           "newPrice":"1.589,00 ₺","oldPrice":"","outOfStock":true,
           "slug":"sleepy-bear-2-li-paket","normalImg":"/media/uploads/images/sleepy.png"},
          {"id":40,"stock_code":"VTB99","name":"Vita Bear Bamboo Saç Fırçası",
           "newPrice":"499,00 ₺","oldPrice":"","outOfStock":true,
           "slug":"vita-bear-bamboo-sac-fircasi","normalImg":"/media/uploads/images/firca.png"},
          {"id":41,"stock_code":"VTB98","name":"Vita Bear Sleepy Bear Uyku Bandı",
           "newPrice":"50,00 ₺","oldPrice":"","outOfStock":true,
           "slug":"sleepy-bear-uyku-bandi","normalImg":"/media/uploads/images/band.png"}
        ]
        """;

    [Fact]
    public void KatalogdanUrunleriOkur()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        var hair = Assert.Single(urunler, u => u.Name.Contains("Hair Plus"));
        Assert.Equal("Vita Bear Hair Plus Vegan Saç Vitamini", hair.Name);
        Assert.Equal(1145.00m, hair.Price);
        Assert.True(hair.InStock);
        Assert.Equal("https://www.vitabear.com.tr/all/products/hair-plus-vegan-sac-vitamini", hair.Url);
        // Markanın KENDİ sitesi: Seller null kalmalı.
        Assert.Null(hair.Seller);
        // Tek markalı kaynak — marka scraper'ın kendisinden geliyor.
        Assert.Null(hair.BrandName);
        // Sitede indirim yoktu; alan boş olduğu için okunmamalı.
        Assert.Null(hair.StoreOldPrice);
    }

    // "1.145,00 ₺" — nokta BİNLİK ayraç, virgül ondalık. Ayrıca kaynak "TL"
    // değil "₺" simgesi kullanıyor; TurkishPriceParser tek başına bunu atmaz.
    [Fact]
    public void TurkLiraSimgesiVeBinlikAyracDogruAyristirilir()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        Assert.Equal(1145.00m, urunler.Single(u => u.Name.Contains("Hair Plus")).Price);
        Assert.Equal(1589.00m, urunler.Single(u => u.Name.Contains("2'li Paket")).Price);
    }

    // Görsel yolunda BOŞLUK ve Türkçe harf var; kodlanmadan istenirse
    // adres geçersiz olur.
    [Fact]
    public void GorselAdresiYuzdeliKodlanir()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        Assert.Equal(
            "https://www.vitabear.com.tr/media/uploads/images/Hair%20Plus%20Ay%C4%B1c%C4%B1k%20170965131855136c819.png",
            urunler.Single(u => u.Name.Contains("Hair Plus")).ImageUrl);
    }

    [Fact]
    public void StokDurumuTersCevrilir()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        Assert.True(urunler.Single(u => u.Name.Contains("Hair Plus")).InStock);
        Assert.False(urunler.Single(u => u.Name.Contains("2'li Paket")).InStock);
    }

    // Alan hiç yoksa "stokta yok" DEĞİL, "bilmiyoruz" demek.
    [Fact]
    public void StokAlaniYoksaBosBirakilir()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(
            """[{"name":"Vita Bear Cilt Vitamini","newPrice":"985,00 ₺","slug":"cilt"}]""",
            BaseUrl);

        Assert.Null(Assert.Single(urunler).InStock);
    }

    // Katalogdaki iki aksesuar: saç fırçası ve uyku bandı. İkisi de ortak
    // süzgeçte YOK, bu kaynağa özel kalıpla eleniyor.
    [Fact]
    public void SacFircasiVeUykuBandiSuzulur()
    {
        var (urunler, suzulen) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        Assert.Equal(2, suzulen);
        Assert.DoesNotContain(urunler, u => u.Name.Contains("Fırça"));
        Assert.DoesNotContain(urunler, u => u.Name.Contains("Uyku Bandı"));
        Assert.Equal(2, urunler.Count);
    }

    // Türkçe tuzağı: invariant IgnoreCase "I" ile "ı"yı eşleştirmez, yani
    // tamamı büyük yazılmış bir ad kalıba takılmazdı.
    [Theory]
    [InlineData("Vita Bear Bamboo Saç Fırçası")]
    [InlineData("VITA BEAR BAMBOO SAÇ FIRÇASI")]
    [InlineData("Vita Bear Bamboo Sac Fircasi")]
    [InlineData("Vita Bear Sleepy Bear Uyku Bandı")]
    [InlineData("VITA BEAR SLEEPY BEAR UYKU BANDI")]
    public void AksesuarKalibiYazimFarklariniYakalar(string ad)
    {
        var json = $$"""[{"name":"{{ad}}","newPrice":"499,00 ₺","slug":"x"}]""";

        var (urunler, suzulen) = VitabearScraper.ParseCatalog(json, BaseUrl);

        Assert.Empty(urunler);
        Assert.Equal(1, suzulen);
    }

    // Gerçek vitaminler elenmemeli — özellikle adında "band"/"fırça"ya
    // benzeyen bir şey geçmeyenler.
    [Theory]
    [InlineData("Vita Bear Relax Bear")]
    [InlineData("Vita Bear Magbear Magnezyum Kompleks Vegan Kapsül")]
    [InlineData("Kids Sleepy Bear Çocuk Uyku Düzenleyici Vitamin")]
    [InlineData("Vita Bear Muhteşem İkili Set")]
    public void GercekVitaminlerElenmez(string ad)
    {
        var json = $$"""[{"name":"{{ad}}","newPrice":"899,00 ₺","slug":"x"}]""";

        var (urunler, suzulen) = VitabearScraper.ParseCatalog(json, BaseUrl);

        Assert.Single(urunler);
        Assert.Equal(0, suzulen);
    }

    // Kaynağın kendi kategorisi 18 üründe de "vitaminler" (fırçada bile),
    // o yüzden kullanılmıyor; kategori scraper'da sabitleniyor. Parser'a
    // bırakılsaydı "Relax Bear" gibi adlar kategorisiz kalırdı.
    [Fact]
    public void KategoriScraperdaVeriliyor()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        Assert.All(urunler, u => Assert.Equal("vitamin", u.Category));
    }

    // Kaynak `numericPrice`ı float artığıyla veriyor
    // (1144,9999999999997726263245568). O alan okunsaydı fiyat yanlış olurdu.
    [Fact]
    public void BozukNumericPriceAlaniKullanilmaz()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(Katalog, BaseUrl);

        Assert.Equal(1145.00m, urunler.Single(u => u.Name.Contains("Hair Plus")).Price);
    }

    [Fact]
    public void MagazaIndirimiVarsaOkunur()
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(
            """[{"name":"Vita Bear Cilt Vitamini","newPrice":"985,00 ₺","oldPrice":"1.200,00 ₺","slug":"cilt"}]""",
            BaseUrl);

        Assert.Equal(1200.00m, Assert.Single(urunler).StoreOldPrice);
    }

    [Theory]
    [InlineData("""[{"name":"Vita Bear Cilt Vitamini","newPrice":"","slug":"cilt"}]""")]
    [InlineData("""[{"name":"Vita Bear Cilt Vitamini","slug":"cilt"}]""")]
    [InlineData("""[{"name":"Vita Bear Cilt Vitamini","newPrice":"bedava","slug":"cilt"}]""")]
    [InlineData("""[{"newPrice":"985,00 ₺","slug":"cilt"}]""")]
    [InlineData("""[{"name":"Vita Bear Cilt Vitamini","newPrice":"985,00 ₺"}]""")]
    public void EksikVeyaBozukKayitAtlanir(string json)
    {
        var (urunler, _) = VitabearScraper.ParseCatalog(json, BaseUrl);

        Assert.Empty(urunler);
    }

    [Theory]
    [InlineData("bozuk json")]
    [InlineData("""{"message":""}""")]
    public void BeklenmeyenGovdeTaramayiDusurmez(string json)
    {
        var (urunler, suzulen) = VitabearScraper.ParseCatalog(json, BaseUrl);

        Assert.Empty(urunler);
        Assert.Equal(0, suzulen);
    }
}

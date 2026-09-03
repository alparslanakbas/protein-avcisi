using IndirimTakip.Infrastructure.Scraping.Protein34;

namespace IndirimTakip.Infrastructure.Tests;

// Örnek blok protein34.com'un gerçek çıktısından alındı (3 Eylül 2026).
// TIRNAKLAR BİLEREK KARIŞIK: itemprop tek, content çift — kaynak gerçekten
// böyle yazıyor ve ilk regex denemesi bu yüzden hiçbir fiyatı okuyamamıştı.
public class Protein34ScraperTests
{
    private const string Sayfa = """
        <h1 class="product-title">Hardline BCAA 4:1:1 300 Gr Aromasız 60 Servis</h1>
        <script>var product = {brand: "3",brandName: "Hardline",variant: 0};</script>
        <meta itemprop='price' content="879.00" />
        <link itemprop='availability' href='https://schema.org/InStock' />
        <img itemprop="image" alt="Hardline BCAA" src="//www.protein34.com/idea/mi/46/hardline-bcaa.jpg" />
        <div class="product-price-old">879,00 TL</div>
        """;

    [Fact]
    public void UrunuOkur()
    {
        var p = Protein34Scraper.ParseProduct(Sayfa, "https://www.protein34.com/urun/hardline-bcaa");

        Assert.NotNull(p);
        Assert.Equal("Hardline BCAA 4:1:1 300 Gr Aromasız 60 Servis", p.Name);
        Assert.Equal(879.00m, p.Price);
        Assert.True(p.InStock);
        Assert.Equal("Hardline", p.BrandName);
        Assert.Equal("https://www.protein34.com/idea/mi/46/hardline-bcaa.jpg", p.ImageUrl);
        // BAYİ kaydı: Seller dolu olmalı, yoksa markanın kendi sitesinden
        // gelen üründen ayrılamaz ve marka sayfası kendi vitrinini gösteremez.
        Assert.Equal("protein34.com", p.Seller);
        Assert.Null(p.Category);
    }

    // Sayfadaki "eski fiyat" kutusu güncel fiyatın AYNISINI yazıyor.
    // Okunsaydı olmayan bir "mağaza indirimi" üretirdi — sitenin iddiası
    // gerçek fiyat geçmişine dayandığı için tam da kaçınılan şey.
    [Fact]
    public void MagazaEskiFiyatiAlinmaz()
    {
        var p = Protein34Scraper.ParseProduct(Sayfa, "https://x");

        Assert.Null(p!.StoreOldPrice);
    }

    // Fiyat NOKTA ondalıklı; Türkçe kültürle ayrıştırılsaydı 87900 çıkardı.
    [Fact]
    public void NoktaliOndaligiBinlikAyracSanmaz()
    {
        var p = Protein34Scraper.ParseProduct(
            Sayfa.Replace("content=\"879.00\"", "content=\"1299.50\""), "https://x");

        Assert.Equal(1299.50m, p!.Price);
    }

    [Fact]
    public void StokDurumuOkunuyor()
    {
        var tukendi = Protein34Scraper.ParseProduct(
            Sayfa.Replace("schema.org/InStock", "schema.org/OutOfStock"), "https://x");

        Assert.False(tukendi!.InStock);
    }

    // Alan yoksa "stokta yok" DEĞİL, "bilmiyoruz".
    [Fact]
    public void StokAlaniYoksaBosBirakilir()
    {
        var p = Protein34Scraper.ParseProduct(
            Sayfa.Replace("<link itemprop='availability' href='https://schema.org/InStock' />", ""),
            "https://x");

        Assert.Null(p!.InStock);
    }

    // Görsel adresi protokolsüz geliyor ("//www.protein34.com/...").
    [Fact]
    public void ProtokolsuzGorselAdresiTamamlanir()
    {
        var p = Protein34Scraper.ParseProduct(Sayfa, "https://x");

        Assert.StartsWith("https://", p!.ImageUrl);
    }

    // Marka adları kanonik yazıma çevrilmeli; çevrilmezse kopya Brand
    // kaydı oluşur. "KEVİN LEVRONE" ayrıca Türkçe NOKTALI İ taşıyor ve
    // sözlük OrdinalIgnoreCase olduğu için birebir anahtar gerekiyor.
    [Theory]
    [InlineData("Bigjoy Sports", "BigJoy")]
    [InlineData("Nuclear", "Nuclear Nutrition")]
    [InlineData("KEVİN LEVRONE", "Kevin Levrone")]
    [InlineData("Optimum", "Optimum Nutrition")]
    [InlineData("Zero Shot", "ZeroShot")]
    [InlineData("Hardline", "Hardline")]
    public void MarkaAdlariKanoniklestirilir(string kaynak, string beklenen)
    {
        var p = Protein34Scraper.ParseProduct(
            Sayfa.Replace("brandName: \"Hardline\"", $"brandName: \"{kaynak}\""), "https://x");

        Assert.Equal(beklenen, p!.BrandName);
    }

    [Fact]
    public void AksesuarElenir()
    {
        var p = Protein34Scraper.ParseProduct(
            Sayfa.Replace("Hardline BCAA 4:1:1 300 Gr Aromasız 60 Servis", "BodyMax Shaker 700ml"),
            "https://x");

        Assert.Null(p);
    }

    [Theory]
    [InlineData("<html><body>ad yok</body></html>")]
    [InlineData("<h1>Bir Ürün</h1>")]  // fiyat yok
    public void EksikVeriliSayfaNullDoner(string html)
    {
        Assert.Null(Protein34Scraper.ParseProduct(html, "https://x"));
    }

    [Fact]
    public void SifirFiyatliKayitAlinmaz()
    {
        var p = Protein34Scraper.ParseProduct(
            Sayfa.Replace("content=\"879.00\"", "content=\"0.00\""), "https://x");

        Assert.Null(p);
    }
}

using IndirimTakip.Infrastructure.Scraping.S4u;

namespace IndirimTakip.Infrastructure.Tests;

// Örnek blok s4u.com.tr'nin gerçek çıktısından alındı (2 Eylül 2026).
public class S4uScraperTests
{
    private const string Sayfa = """
        <script type="application/ld+json">
        {"@context":"https://schema.org","@type":"Product",
         "name":"S4U Strake Series Creatine 300g - Aromasız Mikronize Kreatin",
         "image":"https://s4u.com.tr/image/cache/catalog/urun/creatine.jpg",
         "offers":{"@type":"Offer","priceCurrency":"TRY","price":"549.00",
         "availability":"https://schema.org/InStock"}}
        </script>
        """;

    [Fact]
    public void SchemaOrgBlogundanUrunuOkur()
    {
        var p = S4uScraper.ParseProduct(Sayfa, "https://s4u.com.tr/index.php?route=product/product&product_id=20");

        Assert.NotNull(p);
        Assert.Equal("S4U Strake Series Creatine 300g - Aromasız Mikronize Kreatin", p.Name);
        Assert.Equal(549.00m, p.Price);
        Assert.True(p.InStock);
        Assert.Equal("https://s4u.com.tr/image/cache/catalog/urun/creatine.jpg", p.ImageUrl);
        // Markanın KENDİ sitesi: Seller null kalmalı, yoksa bayi kaydından
        // ayrılamaz ve marka sayfası kendi vitrinini gösteremez.
        Assert.Null(p.Seller);
        // Kategori isimden çıkarılıyor, kaynaktan alınmıyor.
        Assert.Null(p.Category);
    }

    // schema.org fiyatı NOKTA ondalıklı ("549.00"). Türkçe kültürle
    // ayrıştırılsaydı 54900 çıkardı — bu tuzağa projede daha önce düşüldü.
    [Fact]
    public void NoktaliOndaligiBinlikAyracSanmaz()
    {
        var p = S4uScraper.ParseProduct(
            Sayfa.Replace("\"549.00\"", "\"1299.50\""),
            "https://s4u.com.tr/x");

        Assert.NotNull(p);
        Assert.Equal(1299.50m, p.Price);
    }

    [Fact]
    public void StokDurumuOkunuyor()
    {
        var tukendi = S4uScraper.ParseProduct(
            Sayfa.Replace("schema.org/InStock", "schema.org/OutOfStock"),
            "https://s4u.com.tr/x");

        Assert.NotNull(tukendi);
        Assert.False(tukendi.InStock);
    }

    // Tanımadığımız bir değer "stokta yok" DEĞİL, "bilmiyoruz" demek.
    [Fact]
    public void TanimayanStokDegeriBosBirakilir()
    {
        var p = S4uScraper.ParseProduct(
            Sayfa.Replace("\"availability\":\"https://schema.org/InStock\"", "\"availability\":\"BilinmeyenDeger\""),
            "https://s4u.com.tr/x");

        Assert.NotNull(p);
        Assert.Null(p.InStock);
    }

    [Theory]
    [InlineData("<html><body>Product yok</body></html>")]
    [InlineData("""<script type="application/ld+json">{"@type":"WebSite","name":"S4U"}</script>""")]
    public void UrunBloguYoksaNullDoner(string html)
    {
        Assert.Null(S4uScraper.ParseProduct(html, "https://s4u.com.tr/x"));
    }

    [Fact]
    public void BozukJsonTaramayiDusurmez()
    {
        var html = """<script type="application/ld+json">{bozuk json</script>""" + Sayfa;

        var p = S4uScraper.ParseProduct(html, "https://s4u.com.tr/x");

        Assert.NotNull(p);
        Assert.Equal(549.00m, p.Price);
    }
}

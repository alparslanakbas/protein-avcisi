using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Örnek bloklar gigis.com.tr ve mlaprotein.com'un gerçek çıktısından alındı
// (3 Eylül 2026).
public class IkasSchemaOrgCatalogTests
{
    private const string GigisSayfa = """
        <script type="application/ld+json">
        {"@context":"https://schema.org","@type":"Product",
         "name":"Crunchies Tanışma Paketi 50g",
         "image":["https://cdn.myikas.com/images/6c5f8a8e/377c0144.jpg"],
         "brand":{"@type":"Brand","name":"Gigi's"},
         "offers":[{"@type":"Offer","priceCurrency":"TRY","price":"755.00",
                    "availability":"https://schema.org/InStock"}]}
        </script>
        """;

    private const string MlaSayfa = """
        <script type="application/ld+json">
        {"@context":"https://schema.org","@type":"Product",
         "name":"BCAA 10:1:1",
         "image":["https://cdn.myikas.com/images/4445ff16/8a3b81c7.jpg"],
         "brand":{"@type":"Brand","name":"mla protein"},
         "offers":[{"@type":"Offer","priceCurrency":"TRY","price":"599.00",
                    "availability":"https://schema.org/OutOfStock"}]}
        </script>
        """;

    [Fact]
    public void UrunuOkur()
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(GigisSayfa, "https://gigis.com.tr/x", "Gigi's");

        Assert.NotNull(p);
        Assert.Equal("Crunchies Tanışma Paketi 50g", p.Name);
        Assert.Equal(755.00m, p.Price);
        Assert.True(p.InStock);
        Assert.Equal("https://cdn.myikas.com/images/6c5f8a8e/377c0144.jpg", p.ImageUrl);
        Assert.Equal("Gigi's", p.BrandName);
        // Markanın kendi sitesi — bayi kaydından ayrılabilmesi için null.
        Assert.Null(p.Seller);
        // Kategori isimden çıkarılıyor, kaynaktan alınmıyor.
        Assert.Null(p.Category);
    }

    // schema.org fiyatı NOKTA ondalıklı. Türkçe kültürle ayrıştırılsaydı
    // 75500 çıkardı — bu tuzağa projede daha önce düşüldü.
    [Fact]
    public void NoktaliOndaligiBinlikAyracSanmaz()
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(
            GigisSayfa.Replace("\"755.00\"", "\"1299.50\""), "https://gigis.com.tr/x", "Gigi's");

        Assert.Equal(1299.50m, p!.Price);
    }

    [Fact]
    public void StokDurumuOkunuyor()
    {
        var stokta = IkasSchemaOrgCatalog.ParseProduct(GigisSayfa, "https://x", "Gigi's");
        var tukendi = IkasSchemaOrgCatalog.ParseProduct(MlaSayfa, "https://x", null);

        Assert.True(stokta!.InStock);
        Assert.False(tukendi!.InStock);
    }

    // Tanımadığımız bir değer "stokta yok" DEĞİL, "bilmiyoruz" demek.
    [Fact]
    public void TanimayanStokDegeriBosBirakilir()
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(
            GigisSayfa.Replace("https://schema.org/InStock", "BilinmeyenDeger"), "https://x", "Gigi's");

        Assert.Null(p!.InStock);
    }

    // ÇOK MARKALI mağaza: marka sayfadan okunmalı ve takma ad haritasından
    // geçmeli — "mla protein" küçük harfle geliyor, katalogda "MLA Protein"
    // olmalı, yoksa kopya Brand kaydı oluşur.
    [Fact]
    public void MarkaSayfadanOkunupNormalizeEdilir()
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(MlaSayfa, "https://mlaprotein.com/x");

        Assert.Equal("MLA Protein", p!.BrandName);
    }

    [Fact]
    public void MarkaSabitlenirseSayfadakiYokSayilir()
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(MlaSayfa, "https://x", "Gigi's");

        Assert.Equal("Gigi's", p!.BrandName);
    }

    // Gigi's'te "Kendi Paketini Kendin Yap" 0 TL ile geliyor: yapılandırıcı
    // sayfası, gerçek ürün değil.
    [Theory]
    [InlineData("\"0.00\"")]
    [InlineData("\"0\"")]
    public void SifirFiyatliKayitAlinmaz(string fiyat)
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(
            GigisSayfa.Replace("\"755.00\"", fiyat), "https://x", "Gigi's");

        Assert.Null(p);
    }

    // Aksesuarlar ortak süzgeçten geçiyor.
    [Theory]
    [InlineData("El Yapımı Seramik Kase - Mint")]
    [InlineData("El Yapımı Kuru Yemişlik")]
    [InlineData("Gigi's Keşif Çantası")]
    [InlineData("Protein Shaker")]
    [InlineData("Şekersiz Ketçap (520gr)")]
    [InlineData("BBQ SOS")]
    [InlineData("Sprey Yağ")]
    [InlineData("Aromalı Tatlandırıcı")]
    public void TakviyeDisiUrunlerElenir(string ad)
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(
            GigisSayfa.Replace("Crunchies Tanışma Paketi 50g", ad), "https://x", "Gigi's");

        Assert.Null(p);
    }

    // YANLIŞ POZİTİF KORUMASI: gerçek ürünler elenmemeli. "sos" kalıbı
    // kelime sınırlı ve açık ek listeli olduğu için "SOSis"i yakalamamalı.
    [Theory]
    [InlineData("Açai Orman Meyveli Kolajenli Protein Bar")]
    [InlineData("Whey İzole")]
    [InlineData("Creatine Monohydrate")]
    [InlineData("Yer Fıstıklı Granola")]
    [InlineData("Protein Sosisi")]
    [InlineData("Rice Cream (Pirinç Kreması)")]
    public void GercekUrunlerElenmez(string ad)
    {
        var p = IkasSchemaOrgCatalog.ParseProduct(
            GigisSayfa.Replace("Crunchies Tanışma Paketi 50g", ad), "https://x", "Gigi's");

        Assert.NotNull(p);
        Assert.Equal(ad, p.Name);
    }

    [Theory]
    [InlineData("<html><body>Product yok</body></html>")]
    [InlineData("""<script type="application/ld+json">{"@type":"WebSite","name":"Gigi's"}</script>""")]
    public void UrunBloguYoksaNullDoner(string html)
    {
        Assert.Null(IkasSchemaOrgCatalog.ParseProduct(html, "https://x", "Gigi's"));
    }

    [Fact]
    public void BozukJsonTaramayiDusurmez()
    {
        var html = """<script type="application/ld+json">{bozuk json</script>""" + GigisSayfa;

        var p = IkasSchemaOrgCatalog.ParseProduct(html, "https://x", "Gigi's");

        Assert.Equal(755.00m, p!.Price);
    }

    [Fact]
    public void SitemapAdresleriTekillestirilerekOkunur()
    {
        var xml = """
            <urlset><url><loc>https://gigis.com.tr/a</loc></url>
            <url><loc>https://gigis.com.tr/b</loc></url>
            <url><loc>https://gigis.com.tr/a</loc></url></urlset>
            """;

        var adresler = IkasSchemaOrgCatalog.ParseSitemap(xml);

        Assert.Equal(2, adresler.Count);
        Assert.Contains("https://gigis.com.tr/a", adresler);
        Assert.Contains("https://gigis.com.tr/b", adresler);
    }
}

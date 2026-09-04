using IndirimTakip.Infrastructure.Scraping.MusclePump;

namespace IndirimTakip.Infrastructure.Tests;

// Fixture'lar musclepump.com.tr AKINSOFT ürün sayfalarının 2 Eylül 2026
// tarihli gerçek ana detay bloğu, fiyat ve ürün bilgisi yapısından alınmıştır.
// FİKSTÜR 3 EYLÜL'DE GÜNCELLENDİ. Site mikro veriyi (itemprop/itemscope)
// tamamen kaldırıp JSON-LD'ye geçti ve @graph dizisi kullanıyor; eski
// fikstür gerçeği yansıtmadığı için tarama canlıda sessizce 0 ürün
// veriyordu (bkz. MusclePumpScraper.ParseProduct).
public class MusclePumpScraperTests
{
    [Fact]
    public void IndirimliMusclePumpUrununuAnaDetayBlogundanOkur()
    {
        var html = ProductHtml(
            "Muscle Pump Wpc Instant Whey Çikolata 1950 Gr",
            "MUSCLE PUMP",
            "5490.10",
            "6.169,00 TL",
            "/protein-tozu/whey-protein/prd-muscle-pump-wpc-instant-whey-cikolata-1950-gr-mp-01-0011",
            inStock: true,
            servingText: "SERVİS SAYISI: 65 SERVİS PORSİYON BÜYÜKLÜĞÜ: 30gr");

        var product = MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/eski-adres/prd-eski");

        Assert.NotNull(product);
        Assert.Equal("Muscle Pump Wpc Instant Whey Çikolata 1950 Gr", product.Name);
        Assert.Equal(5490.10m, product.Price);
        Assert.Equal(6169m, product.StoreOldPrice);
        Assert.Equal("protein-tozu", product.Category);
        Assert.Equal(65, product.ServingsPerPackage);
        Assert.Equal(30m, product.ServingSizeGrams);
        Assert.True(product.InStock);
        Assert.Null(product.BrandName);
        Assert.Null(product.Seller);
    }

    [Fact]
    public void SygenixUrunundeUreticiVeSaticiyiAyirir()
    {
        var html = ProductHtml(
            "Sygenix Bcaa Ananas 300 gr",
            "SYGENIX",
            "695.00",
            null,
            "/amino-asitler/toz-bcaa/prd-sygenix-bcaa-ananas-300-gr-sy-01-01",
            inStock: true,
            servingText: "SERVİS SAYISI: 30 SERVİS PORSİYON BÜYÜKLÜĞÜ: 10gr");

        var product = MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/amino-asitler/prd-sygenix");

        Assert.NotNull(product);
        Assert.Equal("Sygenix", product.BrandName);
        Assert.Equal("musclepump.com.tr", product.Seller);
        Assert.Equal("amino-asitler", product.Category);
        Assert.Equal(695m, product.Price);
    }

    [Fact]
    public void StokDisiUrunuFiyatiylaKorumayaDevamEder()
    {
        var html = ProductHtml(
            "Muscle Pump Creatine 300 Gr",
            "MUSCLE PUMP",
            "899.00",
            null,
            "/performansguc/kreatin/prd-muscle-pump-creatine-300-gr",
            inStock: false);

        var product = MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/performansguc/kreatin/prd-creatine");

        Assert.NotNull(product);
        Assert.False(product.InStock);
        Assert.Equal("kreatin", product.Category);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("geçersiz")]
    public void SifirVeyaGecersizFiyatiAlmaz(string price)
    {
        var html = ProductHtml(
            "Muscle Pump Creatine 300 Gr",
            "MUSCLE PUMP",
            price,
            null,
            "/performansguc/kreatin/prd-muscle-pump-creatine-300-gr",
            inStock: true);

        Assert.Null(MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/performansguc/kreatin/prd-creatine"));
    }

    [Theory]
    [InlineData("Muscle Pump Demir Stand", "/fitness-aksesuarlari/stant/prd-muscle-pump-demir-stand-3272")]
    [InlineData("Muscle Pump Masaüstü Saşe Dolu Stand", "/kombinasyon/stand/prd-muscle-pump-masaustu-sase-dolu-stand-mus-001")]
    public void AksesuarVeStandlariParserSeviyesindeDeAtlar(string name, string path)
    {
        var html = ProductHtml(
            name,
            "MUSCLE PUMP",
            "3999.00",
            null,
            path,
            inStock: true);

        Assert.Null(MusclePumpScraper.ParseProduct(html, $"https://musclepump.com.tr{path}"));
    }

    [Fact]
    public void TekKullanimlikPreVenomuPreWorkoutOlarakSiniflandirir()
    {
        var html = ProductHtml(
            "Muscle Pump Pre-Venom Powder Ananas 17 Gr",
            "MUSCLE PUMP",
            "65.00",
            null,
            "/tek-kullanim/prd-muscle-pump-pre-venom-powder-ananas-17-gr-mp05-01-01",
            inStock: true);

        var product = MusclePumpScraper.ParseProduct(
            html,
            "https://musclepump.com.tr/tek-kullanim/prd-muscle-pump-pre-venom");

        Assert.NotNull(product);
        Assert.Equal("pre-workout", product.Category);
    }

    [Fact]
    public void SitemapYalnizTakviyeUrunAdresleriniTekillestirir()
    {
        const string xml = """
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://musclepump.com.tr/protein-tozu/whey-protein/prd-whey</loc></url>
              <url><loc>https://www.musclepump.com.tr/protein-tozu/whey-protein/prd-whey</loc></url>
              <url><loc>https://musclepump.com.tr/fitness-aksesuarlari/shaker/prd-shaker</loc></url>
              <url><loc>https://musclepump.com.tr/kombinasyon/stand/prd-masaustu-stand</loc></url>
              <url><loc>https://baska-site.example/protein-tozu/prd-sahte</loc></url>
              <url><loc>http://musclepump.com.tr/protein-tozu/prd-guvensiz</loc></url>
              <url><loc>https://musclepump.com.tr/sayfa/hakkimizda</loc></url>
            </urlset>
            """;

        var urls = MusclePumpScraper.ParseSitemapUrls(xml);

        Assert.Equal(["https://musclepump.com.tr/protein-tozu/whey-protein/prd-whey"], urls);
    }

    [Fact]
    public void IlgiliUrunFiyatiYerineAnaDetayFiyatiniOkur()
    {
        var html = ProductHtml(
            "Muscle Pump Join Bcaa +Glutamine 480 Gr",
            "MUSCLE PUMP",
            "1290.00",
            null,
            "/amino-asitler/glutamine/prd-muscle-pump-join-bcaa-glutamine-480-gr",
            inStock: true) + """
            <productitem itemscope itemtype="https://schema.org/Product">
              <div class="productPrice"><meta itemprop="price" content="39.00" /></div>
            </productitem>
            """;

        var product = MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/amino-asitler/prd-join");

        Assert.NotNull(product);
        Assert.Equal(1290m, product.Price);
    }

    // Sitenin JSON-LD'si düz bir Product nesnesi DEĞİL, @graph dizisi:
    // WebSite, BreadcrumbList ve Product aynı blokta duruyor. Düz nesne
    // varsayan bir okuyucu Product'ı hiç bulamaz — canlıda tam bu oldu ve
    // tarama "136 adres, 0 ürün" verdi.
    [Fact]
    public void GrafDizisiIcindekiUrunDugumunuBulur()
    {
        var html = ProductHtml("Sygenix Complex Whey", "Sygenix", "3589.76", null, "/prd-x", inStock: true);

        var product = MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/prd-x");

        Assert.NotNull(product);
        Assert.Equal("Sygenix Complex Whey", product.Name);
        Assert.Equal(3589.76m, product.Price);
    }

    // Fiyat JSON-LD'de NOKTA ondalıklı geliyor; Türkçe kültürle
    // ayrıştırılsaydı 358976 çıkardı.
    [Fact]
    public void NoktaliOndaligiBinlikAyracSanmaz()
    {
        var html = ProductHtml("Test", "Sygenix", "1299.50", null, "/prd-x", inStock: true);

        var product = MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/prd-x");

        Assert.Equal(1299.50m, product!.Price);
    }

    // JSON-LD hiç yoksa (ya da Product düğümü yoksa) ürün üretilmemeli —
    // uydurma veri yerine boş geçmek doğru davranış.
    [Fact]
    public void SchemaOrgYoksaNullDoner()
    {
        const string html = """
            <html><head><link rel="canonical" href="https://musclepump.com.tr/prd-x" /></head>
            <body><detail-region><div class="detailRightBlock"></div></detail-region></body></html>
            """;

        Assert.Null(MusclePumpScraper.ParseProduct(html, "https://musclepump.com.tr/prd-x"));
    }

    private static string ProductHtml(
        string name,
        string brand,
        string price,
        string? oldPrice,
        string canonicalPath,
        bool inStock,
        string servingText = "")
    {
        var oldPriceHtml = oldPrice is null ? string.Empty : $"<strike>{oldPrice}</strike>";
        var button = inStock
            ? "<button class=\"baketButton\" data-basket-add=\"true\">Sepete Ekle</button>"
            : "<button class=\"baketButton\" data-basket-add=\"true\" disabled=\"disabled\">Stokta Bulunmuyor</button>";

        return $$$"""
            <html><head>
              <link rel="canonical" href="https://musclepump.com.tr{{{canonicalPath}}}" />
              <meta property="og:image" content="https://musclepump.com.tr/thumb.ashx?Resim=/Resim/ornek.jpeg&amp;v=1" />
              <script type="application/ld+json">
              {"@context":"https://schema.org","@graph":[
                {"@type":"WebSite","name":"Muscle Pump"},
                {"@type":"BreadcrumbList","itemListElement":[]},
                {"@type":"Product","name":"{{{name}}}",
                 "brand":{"@type":"Brand","name":"{{{brand}}}"},
                 "image":["https://musclepump.com.tr/Resim/ornek.png"],
                 "offers":{"@type":"Offer","price":"{{{price}}}","priceCurrency":"TRY",
                           "availability":"https://schema.org/InStock"}}
              ]}
              </script>
            </head><body>
              <detail-region>
                <div class="detailRightBlock basket4selector">
                  <div class="detailPriceBlock">
                    {{{oldPriceHtml}}}
                  </div>
                  <div class="basketButtonBlock">{{{button}}}</div>
                </div>
                <div id="nav-description">{{{servingText}}}</div>
              </detail-region>
            </body></html>
            """;
    }
}

using IndirimTakip.Infrastructure.Scraping.SpaceSupplements;

namespace IndirimTakip.Infrastructure.Tests;

// Fixture'lar spacegymsupplements.com ürün sayfalarının 2 Eylül 2026 tarihli
// gerçek schema.org alanlarından ve içerik/bakım metinlerinden alınmıştır.
public class SpaceSupplementsScraperTests
{
    [Theory]
    [InlineData("Galactic Whey", "Her porsiyonunda 21 gram protein içeren whey protein tozu.", "3299", "protein-tozu", 30)]
    [InlineData("Supernova Bulk", "Bulk dönemlerinde ek kalori sağlayan mass gainer.", "2099", "kilo-hacim", 100)]
    [InlineData("Crea Nova", "Her serviste 3000 mg kreatin monohydrate içerir.", "709", "kreatin", 5)]
    [InlineData("Cosmic Chain", "5000 mg lösin içeren güçlü amino asit profili.", "729", "amino-asitler", 10)]
    [InlineData("Galactimine", "Her serviste 5000 mg saf L-glutamin sunar.", "719", "amino-asitler", 10)]
    public void BesGercekTakviyeyiKategoriVeServisiyleOkur(
        string name,
        string description,
        string price,
        string category,
        decimal servingSize)
    {
        var html = ProductHtml(name, description, price, "InStock", servingSize);

        var product = SpaceSupplementsScraper.ParseProduct(
            html,
            $"https://spacegymsupplements.com/urunler/{name.ToLowerInvariant().Replace(' ', '-')}");

        Assert.NotNull(product);
        Assert.Equal(name, product.Name);
        Assert.Equal(decimal.Parse(price), product.Price);
        Assert.Equal(category, product.Category);
        Assert.Equal(servingSize, product.ServingSizeGrams);
        Assert.True(product.InStock);
        Assert.Equal(description, product.Description);
        Assert.Equal("https://cdn.spacegymsupplements.com/media/ornek.jpg", product.ImageUrl);
    }

    [Theory]
    [InlineData("Space Shaker")]
    [InlineData("Space Çanta")]
    public void AksesuarlariAtlar(string name)
    {
        var html = ProductHtml(name, "Aksesuar", "225", "InStock", 0, includeServing: false);

        Assert.Null(SpaceSupplementsScraper.ParseProduct(html, "https://spacegymsupplements.com/urunler/aksesuar"));
    }

    [Fact]
    public void StokDisiUrunuFiyatiylaKorumayaDevamEder()
    {
        var html = ProductHtml("Crea Nova", "Kreatin monohydrate.", "709", "OutOfStock", 5);

        var product = SpaceSupplementsScraper.ParseProduct(html, "https://spacegymsupplements.com/urunler/crea-nova");

        Assert.NotNull(product);
        Assert.False(product.InStock);
        Assert.Equal(709m, product.Price);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("geçersiz")]
    public void SifirVeyaGecersizFiyatiAlmaz(string price)
    {
        var html = ProductHtml("Galactic Whey", "Whey protein.", price, "InStock", 30);

        Assert.Null(SpaceSupplementsScraper.ParseProduct(html, "https://spacegymsupplements.com/urunler/galactic-whey"));
    }

    [Fact]
    public void BozukJsonBlogundanSonraGecerliProductBlogunuBulur()
    {
        var html = """
            <script type="application/ld+json">{bozuk-json}</script>
            """ + ProductHtml("Galactic Whey", "Whey protein.", "3299", "InStock", 30);

        var product = SpaceSupplementsScraper.ParseProduct(html, "https://spacegymsupplements.com/urunler/galactic-whey");

        Assert.NotNull(product);
        Assert.Equal("Galactic Whey", product.Name);
    }

    [Fact]
    public void SitemapYalnizSpaceUrunAdresleriniTekillestirir()
    {
        const string xml = """
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://spacegymsupplements.com/urunler/galactic-whey</loc></url>
              <url><loc>https://spacegymsupplements.com/urunler/galactic-whey</loc></url>
              <url><loc>https://spacegymsupplements.com/kategori/protein</loc></url>
              <url><loc>https://baska-site.example/urunler/sahte</loc></url>
              <url><loc>http://spacegymsupplements.com/urunler/guvensiz</loc></url>
            </urlset>
            """;

        var urls = SpaceSupplementsScraper.ParseSitemapUrls(xml);

        Assert.Equal(["https://spacegymsupplements.com/urunler/galactic-whey"], urls);
    }

    private static string ProductHtml(
        string name,
        string description,
        string price,
        string availability,
        decimal servingSize,
        bool includeServing = true)
    {
        var serving = includeServing
            ? $"<div data-panel=\"care\">SERVİS: 1 Ölçek ( {servingSize} G ) _ 200 ml su</div>"
            : string.Empty;

        return $$$"""
            <script type="application/ld+json">
            {"@context":"https://schema.org","@type":"Product",
             "name":"{{{name}}}","description":"{{{description}}}",
             "image":"https://cdn.spacegymsupplements.com/media/ornek.jpg",
             "brand":{"@type":"Brand","name":"Space"},
             "offers":{"@type":"Offer","price":"{{{price}}}","priceCurrency":"TRY",
             "availability":"https://schema.org/{{{availability}}}"}}
            </script>
            {{{serving}}}
            """;
    }
}

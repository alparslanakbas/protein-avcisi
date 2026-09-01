using IndirimTakip.Infrastructure.Scraping.Nois;

namespace IndirimTakip.Infrastructure.Tests;

// Örnekler nois.com public İkas kataloğunun 2 Eylül 2026 tarihli gerçek
// ürün/kategori/fiyat alanlarından alınmıştır.
public class NoisScraperTests
{
    private readonly NoisScraper scraper = new(new HttpClient());

    [Fact]
    public void VitrindekiIndirimliFiyatiVeListeFiyatiniAyirir()
    {
        var result = scraper.Convert(Product(
            "Nois Whey Rex 900G Protein Tozu - Butter Brownie",
            "nois-whey-rex-900g-protein-tozu",
            2099m,
            1049.5m,
            10,
            "Protein"));

        Assert.NotNull(result);
        Assert.Equal(1049.5m, result.Price);
        Assert.Equal(2099m, result.StoreOldPrice);
        Assert.Equal("protein-tozu", result.Category);
        Assert.True(result.InStock);
        Assert.Null(result.BrandName);
        Assert.Null(result.Seller);
    }

    [Theory]
    [InlineData("NOIS HAVLU \"GRAY\"")]
    [InlineData("NOIS PILL BOX \"BLACK\"")]
    [InlineData("NOIS SHAKER \"WHITE\"")]
    public void AksesuarKategorisiniAtlar(string name)
    {
        Assert.Null(scraper.Convert(Product(name, "aksesuar", 169.4m, 152.5m, 10, "Aksesuar")));
    }

    [Fact]
    public void CantaloupeAromasiniCantaSanipElemez()
    {
        var result = scraper.Convert(Product(
            "Nois Whey Rex 900G Protein Tozu - Cantaloupe",
            "nois-whey-rex-900g-protein-tozu",
            2099m,
            1049.5m,
            10,
            "Protein"));

        Assert.NotNull(result);
        Assert.Equal("protein-tozu", result.Category);
    }

    [Fact]
    public void AksesuarOlmayanPaketiKapsamdaTutar()
    {
        var result = scraper.Convert(Product("NOIS FULL PAKET", "nois-full-paket", 5484m, 2742m, 10, "Paketler"));

        Assert.NotNull(result);
        Assert.Equal(2742m, result.Price);
    }

    [Fact]
    public void StokDisiUrunuFiyatGecmisiIcinKorur()
    {
        var result = scraper.Convert(Product(
            "Nois V-rex Vegan Protein Tozu 1000G 33 Servis - Bebe Bisküvisi",
            "nois-v-rex-vegan-protein",
            1099m,
            549.5m,
            0,
            "Protein"));

        Assert.NotNull(result);
        Assert.False(result.InStock);
        Assert.Equal(33, result.ServingsPerPackage);
    }

    [Theory]
    [InlineData(0.0, null)]
    [InlineData(0.0, 0.0)]
    public void GecerliFiyatiOlmayanUrunuAtlar(double sellPrice, double? discountPrice)
    {
        Assert.Null(scraper.Convert(Product(
            "Nois Whey",
            "nois-whey",
            Convert.ToDecimal(sellPrice),
            discountPrice is null ? null : Convert.ToDecimal(discountPrice.Value),
            5,
            "Protein")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(549.0)]
    [InlineData(600.0)]
    public void GecerliIndirimYoksaSatisFiyatiniKullanir(double? discountPrice)
    {
        var result = scraper.Convert(Product(
            "Nois Whey",
            "nois-whey",
            549m,
            discountPrice is null ? null : Convert.ToDecimal(discountPrice.Value),
            5,
            "Protein"));

        Assert.NotNull(result);
        Assert.Equal(549m, result.Price);
        Assert.Null(result.StoreOldPrice);
    }

    [Fact]
    public void UcuncuTarafUrunundeMarkaVeSaticiyiAyirir()
    {
        var product = Product("RICE CAKE PİRİNÇ PATLAĞI 135 GR 12'li Paket", "rice-cake", 699m, 349.5m, 10, "PİRİNÇ PATLAKLARI");
        product.Brand = new NoisBrand { Name = "ORGANIKSATINAL" };

        var result = scraper.Convert(product);

        Assert.NotNull(result);
        Assert.Equal("OrganikSatınAl", result.BrandName);
        Assert.Equal("nois.com", result.Seller);
        Assert.Equal("saglikli-atistirmaliklar", result.Category);
    }

    [Fact]
    public void AyniSlugdakiAromalariIsimleriyleKorur()
    {
        var first = scraper.Convert(Product("Nois BCAA - Cool Lime", "nois-bcaa", 829m, 414.5m, 10, "Amino Asit"));
        var second = scraper.Convert(Product("Nois BCAA - Sour Cherry", "nois-bcaa", 829m, 414.5m, 10, "Amino Asit"));

        Assert.Equal(first!.Url, second!.Url);
        Assert.NotEqual(first.Name, second.Name);
    }

    private static NoisProduct Product(
        string name,
        string slug,
        decimal sellPrice,
        decimal? discountPrice,
        int stock,
        params string[] categories) => new()
        {
            Name = name,
            Brand = new NoisBrand { Name = "NOIS NUTRITION" },
            MetaData = new NoisMetaData { Slug = slug },
            Categories = categories.Select(name => new NoisCategory { Name = name }).ToList(),
            Variants =
            [
                new NoisVariant
                {
                    Prices = [new NoisPrice { SellPrice = sellPrice, DiscountPrice = discountPrice }],
                    Images = [new NoisImage { Id = "image-id", FileName = "whey-rex", IsMain = true }],
                    Stocks = [new NoisStock { StockCount = stock }],
                },
            ],
        };
}

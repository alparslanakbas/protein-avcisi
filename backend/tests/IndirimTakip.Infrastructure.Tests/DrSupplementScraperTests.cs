using IndirimTakip.Infrastructure.Scraping.DrSupplement;

namespace IndirimTakip.Infrastructure.Tests;

// Örnekler drsupplement.com.tr public İkas kataloğunun 2 Eylül 2026 tarihli
// gerçek ürün, kategori, marka ve fiyat alanlarından alınmıştır.
public class DrSupplementScraperTests
{
    private readonly DrSupplementScraper scraper = new(new HttpClient());

    [Fact]
    public void IndirimliFiyatIleMagazaEskiFiyatiniAyirir()
    {
        var result = scraper.Convert(Product(
            "350 GR WHEY PROTEİN",
            "350-gr-whey-chocolatedonut-aromali",
            "DR SUPPLEMENT",
            899m,
            699m,
            55,
            "PROTEİN"));

        Assert.NotNull(result);
        Assert.Equal(699m, result.Price);
        Assert.Equal(899m, result.StoreOldPrice);
        Assert.Equal("protein-tozu", result.Category);
        Assert.True(result.InStock);
        Assert.Null(result.BrandName);
        Assert.Null(result.Seller);
        Assert.Equal("https://drsupplement.com.tr/350-gr-whey-chocolatedonut-aromali", result.Url);
    }

    [Theory]
    [InlineData("SMART SCOOP")]
    [InlineData("DRSUPPFAMILY LIMITED EDITION ŞAPKA")]
    [InlineData("SuppMeal Shaker")]
    [InlineData("KUPA BARDAK")]
    public void AksesuarKategorisindekiUrunuAtlar(string name)
    {
        Assert.Null(scraper.Convert(Product(name, "aksesuar", "DR SUPPLEMENT", 199m, 99m, 5, "AKSESUAR")));
    }

    [Fact]
    public void PaketiKapsamdaTutar()
    {
        var result = scraper.Convert(Product(
            "WHEY RICE PAKETİ",
            "whey-rice-paketi",
            "DR SUPPLEMENT",
            2400m,
            1999m,
            10,
            "TÜM PAKETLER"));

        Assert.NotNull(result);
        Assert.Equal(1999m, result.Price);
    }

    [Fact]
    public void HerbinaUrunundeUreticiVeSaticiyiAyirir()
    {
        var result = scraper.Convert(Product(
            "HERBINA OMEGA 3",
            "herbina-omega-3",
            "HERBINA",
            499m,
            null,
            12,
            "VİTAMİN & MİNERAL",
            "Herbina Ürünleri"));

        Assert.NotNull(result);
        Assert.Equal("Herbina", result.BrandName);
        Assert.Equal("drsupplement.com.tr", result.Seller);
        Assert.Equal("vitamin", result.Category);
    }

    [Fact]
    public void StokDisiUrunuFiyatGecmisiIcinKorur()
    {
        var result = scraper.Convert(Product(
            "GLOW UP KOLAJEN+ 30 SERVİS",
            "glow-up-kolajen",
            "DR SUPPLEMENT",
            999m,
            899.1m,
            0,
            "ZAYIFLAMA"));

        Assert.NotNull(result);
        Assert.False(result.InStock);
        Assert.Equal(30, result.ServingsPerPackage);
    }

    [Fact]
    public void KreatiniGenelAminoKategorisineDusurmez()
    {
        var result = scraper.Convert(Product(
            "CREATINE 300 GRAM",
            "creatine-300-gram",
            "DR SUPPLEMENT",
            699m,
            599m,
            10,
            "CREATİN",
            "AMİNO ASİT"));

        Assert.NotNull(result);
        Assert.Equal("kreatin", result.Category);
    }

    [Fact]
    public void AyniSlugdakiPaketSecenekleriniIsimleriyleKorur()
    {
        var first = scraper.Convert(Product(
            "SPORA BAŞLANGIÇ PAKETİ - MANDARIN",
            "spora-baslangic-paketi",
            "DR SUPPLEMENT",
            1999m,
            1599m,
            5));
        var second = scraper.Convert(Product(
            "SPORA BAŞLANGIÇ PAKETİ - COOL LİME",
            "spora-baslangic-paketi",
            "DR SUPPLEMENT",
            1999m,
            1599m,
            5));

        Assert.Equal(first!.Url, second!.Url);
        Assert.NotEqual(first.Name, second.Name);
    }

    [Fact]
    public void IndirimYoksaSatisFiyatiniKullanir()
    {
        var result = scraper.Convert(Product(
            "HERBINA D3K2",
            "herbina-d3k2",
            "HERBINA",
            399m,
            null,
            10,
            "VİTAMİN & MİNERAL"));

        Assert.NotNull(result);
        Assert.Equal(399m, result.Price);
        Assert.Null(result.StoreOldPrice);
    }

    [Fact]
    public void GecerliFiyatiOlmayanUrunuAtlar()
    {
        Assert.Null(scraper.Convert(Product(
            "CREATINE 300 GRAM",
            "creatine-300-gram",
            "DR SUPPLEMENT",
            0m,
            null,
            10,
            "CREATİN")));
    }

    [Fact]
    public void MarkasiOlmayanUrunuTahminEtmez()
    {
        var product = Product("CREATINE 300 GRAM", "creatine", "DR SUPPLEMENT", 699m, 599m, 10, "CREATİN");
        product.Brand = null;

        Assert.Null(scraper.Convert(product));
    }

    private static DrSupplementProduct Product(
        string name,
        string slug,
        string brand,
        decimal sellPrice,
        decimal? discountPrice,
        int stock,
        params string[] categories) => new()
        {
            Name = name,
            Brand = new DrSupplementBrand { Name = brand },
            MetaData = new DrSupplementMetaData { Slug = slug },
            Categories = categories.Select(name => new DrSupplementCategory { Name = name }).ToList(),
            Variants =
            [
                new DrSupplementVariant
                {
                    Prices = [new DrSupplementPrice { SellPrice = sellPrice, DiscountPrice = discountPrice }],
                    Images = [new DrSupplementImage { Id = "image-id", FileName = "product", IsMain = true }],
                    Stocks = [new DrSupplementStock { StockCount = stock }],
                },
            ],
        };
}

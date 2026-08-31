using IndirimTakip.Infrastructure.Scraping.SwissNutrition;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Swiss Nutrition çok markalı bir katalog: kaynaktaki <c>brand</c> alanı her
/// zaman gerçek üreticiyi göstermiyor. Bu kural yanlış çalışırsa siteye
/// olmayan bir marka girer ve o markanın sayfası açılır — geri alması
/// sonradan zor.
/// </summary>
public class SwissNutritionBrandTests
{
    private readonly SwissNutritionScraper scraper = new(new HttpClient());

    [Theory]
    [InlineData("Purevits")]
    [InlineData("Herbina")]
    [InlineData("BioBee")]
    public void GercekUreticiAdiOlduguGibiKalir(string marka)
    {
        Assert.Equal(marka, SwissNutritionScraper.NormalizeBrand(marka, "Herhangi Bir Ürün"));
    }

    [Theory]
    [InlineData("Swiss")]
    [InlineData("swiss")]
    [InlineData("Swiss Nutrition")]
    public void MarkaninKendiUrunleriTekAdAltindaBirlesir(string ham)
    {
        // Katalogda markanın kendi ürünleri iki farklı adla geçiyor;
        // birleştirilmezse sitede iki ayrı marka sayfası oluşur.
        Assert.Equal("Swiss Nutrition", SwissNutritionScraper.NormalizeBrand(ham, "Swiss Whey Protein"));
    }

    [Theory]
    [InlineData("FİTNUT GO COCONUT BAR (40G)")]
    [InlineData("FITNUT FISTIK&HURMA PEANUT BAR (30GR)")]
    [InlineData("fitnut çikolatalı choconut bar")]
    public void OrganikSatinAlEtiketliFitnutUrunleriGercekUreticiyeCevrilir(string urunAdi)
    {
        // "OrganikSatınAl" bir üretici değil, kardeş bir satış sitesinin adı.
        // Bu etiketi taşıyan 7 ürünün hepsi FİTNUT marka protein barı.
        Assert.Equal("FitNut", SwissNutritionScraper.NormalizeBrand("OrganikSatınAl", urunAdi));
    }

    [Fact]
    public void OrganikSatinAlEtiketliBaskaBirUrunAtlanir()
    {
        // Üreticiyi BİLMİYORUZ. Tahmin üretmek ("OrganikSatınAl" diye bir
        // marka yaratmak ya da isimden marka uydurmak) yerine ürün alınmıyor.
        Assert.Null(SwissNutritionScraper.NormalizeBrand("OrganikSatınAl", "Basmati Pirinç-İthal (1500 gr)"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkasiOlmayanUrunAtlanir(string? ham)
    {
        Assert.Null(SwissNutritionScraper.NormalizeBrand(ham, "Siyah Büzgülü Çanta"));
    }

    [Fact]
    public void TakviyeKategorisindekiGercekUrunAlinir()
    {
        var result = scraper.Convert(Product(
            "SWISS CREATINE FUSION 5000 - Portakal",
            "Swiss",
            "swiss-creatine-fusion-5000",
            899m,
            12,
            "Kreatin"));

        Assert.NotNull(result);
        Assert.Equal("Swiss Nutrition", result.BrandName);
        Assert.Equal("https://swissnutrition.com/swiss-creatine-fusion-5000", result.Url);
        Assert.Equal(899m, result.Price);
        Assert.True(result.InStock);
        Assert.Null(result.Seller);
    }

    [Fact]
    public void StoktaOlmayanUrunFiyatGecmisiIcinKorunur()
    {
        var result = scraper.Convert(Product(
            "PUREVITS D3 & K2 Takviye Edici Gıda",
            "Purevits",
            "purevits-d3-k2",
            399m,
            0,
            "Vitaminler"));

        Assert.NotNull(result);
        Assert.False(result.InStock);
        Assert.Equal("swissnutrition.com", result.Seller);
    }

    [Theory]
    [InlineData("SWISS FITNESS PAKETİ", 999, "Avantaj Paketleri")]
    [InlineData("Swiss Nutrition Shaker", 199, "Protein")]
    [InlineData("FitNut Badem Ezmesi", 249, "Gıda")]
    [InlineData("SWISS WHEY", 0, "Whey Protein")]
    public void YanilticiVeyaGecersizUrunlerAtlanir(string ad, decimal fiyat, string kategori)
    {
        Assert.Null(scraper.Convert(Product(ad, "Swiss", "ornek", fiyat, 5, kategori)));
    }

    [Fact]
    public void AyniSlugIleGelenAromalarAdlariylaAyristirilir()
    {
        var portakal = scraper.Convert(Product(
            "SWISS CREATINE FUSION 5000 - Portakal",
            "Swiss",
            "swiss-creatine-fusion-5000",
            899m,
            4,
            "Kreatin"));
        var karpuz = scraper.Convert(Product(
            "SWISS CREATINE FUSION 5000 - Karpuz",
            "Swiss",
            "swiss-creatine-fusion-5000",
            899m,
            3,
            "Kreatin"));

        Assert.Equal(portakal!.Url, karpuz!.Url);
        Assert.NotEqual(portakal.Name, karpuz.Name);
    }

    private static IkasProduct Product(
        string name,
        string brand,
        string slug,
        decimal price,
        int stock,
        params string[] categories) => new()
        {
            Name = name,
            Brand = new IkasBrand { Name = brand },
            MetaData = new IkasMetaData { Slug = slug },
            Categories = categories.Select(c => new IkasCategory { Name = c }).ToList(),
            Variants =
            [
                new IkasVariant
                {
                    Prices = [new IkasPrice { SellPrice = price }],
                    Stocks = [new IkasStock { StockCount = stock }],
                },
            ],
        };
}

using IndirimTakip.Infrastructure.Scraping.Biofitle;

namespace IndirimTakip.Infrastructure.Tests;

// Örnekler biofitle.com public İkas kataloğunun 2 Eylül 2026 tarihli gerçek
// ürün, kategori, fiyat ve stok alanlarından alınmıştır.
public class BiofitleScraperTests
{
    private readonly BiofitleScraper scraper = new(new HttpClient());

    [Theory]
    [InlineData("Tarçınlı Yüksek Proteinli Kahvaltılık Gevrek", "tarcinli-yuksek-proteinli-kahvaltilik-gevrek")]
    [InlineData("Çilek ve Vanilya Aromalı Yüksek Proteinli Kahvaltılık Gevrek", "biofit-cilek-vanilya-aromali-yuksek-proteinli-kahvaltilik-gevrek")]
    [InlineData("Kakaolu Yüksek Proteinli Kahvaltılık Gevrek", "kakaolu-yuksek-proteinli-kahvaltilik-gevrek")]
    public void YuksekProteinliGevregiSaglikliAtistirmaliklaraEkler(string name, string slug)
    {
        var result = scraper.Convert(Product(name, slug, 99m, null, 10, "Kahvaltılık Gevrek", "Tüm Ürünler"));

        Assert.NotNull(result);
        Assert.Equal("saglikli-atistirmaliklar", result.Category);
        Assert.Equal(99m, result.Price);
        Assert.Equal($"https://biofitle.com/{slug}", result.Url);
        Assert.True(result.InStock);
    }

    [Theory]
    [InlineData("Şekersiz Mısır Gevreği", "Kahvaltılık Gevrek")]
    [InlineData("Kakaolu Pirinç Patlağı", "Pirinç Patlağı")]
    [InlineData("Sade Pirinç Patlağı", "Pirinç Patlağı")]
    public void StandartGevrekVePirincPatlaginiAtlar(string name, string category)
    {
        Assert.Null(scraper.Convert(Product(name, "standart-urun", 50m, null, 10, category)));
    }

    [Fact]
    public void YuksekProteinIfadesiOlsaDaYanlisKategoriyiAlmaz()
    {
        Assert.Null(scraper.Convert(Product(
            "Yüksek Proteinli Pirinç Patlağı",
            "yuksek-proteinli-pirinc-patlagi",
            99m,
            null,
            10,
            "Pirinç Patlağı")));
    }

    [Fact]
    public void StokDisiUrunuFiyatGecmisiIcinKorur()
    {
        var result = scraper.Convert(Product(
            "Kakaolu Yüksek Proteinli Kahvaltılık Gevrek",
            "kakaolu-yuksek-proteinli-kahvaltilik-gevrek",
            99m,
            null,
            0,
            "Kahvaltılık Gevrek"));

        Assert.NotNull(result);
        Assert.False(result.InStock);
    }

    [Fact]
    public void MagazaIndirimiGelirseGuncelVeEskiFiyatiAyirir()
    {
        var result = scraper.Convert(Product(
            "Kakaolu Yüksek Proteinli Kahvaltılık Gevrek",
            "kakaolu-yuksek-proteinli-kahvaltilik-gevrek",
            99m,
            79m,
            10,
            "Kahvaltılık Gevrek"));

        Assert.NotNull(result);
        Assert.Equal(79m, result.Price);
        Assert.Equal(99m, result.StoreOldPrice);
    }

    [Fact]
    public void GecerliFiyatiOlmayanUrunuAtlar()
    {
        Assert.Null(scraper.Convert(Product(
            "Kakaolu Yüksek Proteinli Kahvaltılık Gevrek",
            "kakaolu-yuksek-proteinli-kahvaltilik-gevrek",
            0m,
            null,
            10,
            "Kahvaltılık Gevrek")));
    }

    [Fact]
    public void KaynakMarkasiBiofitOlmayanUrunuAlmaz()
    {
        var product = Product(
            "Kakaolu Yüksek Proteinli Kahvaltılık Gevrek",
            "kakaolu-yuksek-proteinli-kahvaltilik-gevrek",
            99m,
            null,
            10,
            "Kahvaltılık Gevrek");
        product.Brand = new BiofitleBrand { Name = "Başka Marka" };

        Assert.Null(scraper.Convert(product));
    }

    private static BiofitleProduct Product(
        string name,
        string slug,
        decimal sellPrice,
        decimal? discountPrice,
        int stock,
        params string[] categories) => new()
        {
            Name = name,
            Brand = new BiofitleBrand { Name = "BİOFİT" },
            MetaData = new BiofitleMetaData { Slug = slug },
            Categories = categories.Select(name => new BiofitleCategory { Name = name }).ToList(),
            Variants =
            [
                new BiofitleVariant
                {
                    Prices = [new BiofitlePrice { SellPrice = sellPrice, DiscountPrice = discountPrice }],
                    Images = [new BiofitleImage { Id = "image-id", FileName = "gevrek", IsMain = true }],
                    Stocks = [new BiofitleStock { StockCount = stock }],
                },
            ],
        };
}

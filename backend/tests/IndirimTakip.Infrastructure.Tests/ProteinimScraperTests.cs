using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.Proteinim;

namespace IndirimTakip.Infrastructure.Tests;

// Kayıtlar proteinim.com'un WooCommerce Store API çıktısından alındı
// (4 Eylül 2026), alan adları birebir.
public class ProteinimScraperTests
{
    private static JsonElement Kayit(string json) => JsonDocument.Parse(json).RootElement;

    private const string Urun = """
        {
          "name": "Olimp Whey Protein Complex 100% / 1800 Gr.",
          "permalink": "https://proteinim.com/urun/olimp-whey-protein-complex-100-1800-gr/",
          "is_in_stock": true,
          "prices": {
            "price": "180000",
            "regular_price": "200000",
            "sale_price": "180000",
            "currency_code": "TRY",
            "currency_minor_unit": 2
          },
          "brands": [{ "id": 12, "name": "Olimp", "slug": "olimp" }],
          "images": [{ "src": "https://proteinim.com/wp-content/uploads/olimp.jpg" }]
        }
        """;

    [Fact]
    public void FiyatiKurustanCevirir()
    {
        var p = ProteinimScraper.ParseProduct(Kayit(Urun))!;

        // API tam sayı veriyor ve ölçeği currency_minor_unit söylüyor:
        // "180000" + 2 = 1.800,00 TL. Doğrudan okunsaydı 100 kat şişerdi.
        Assert.Equal(1800.00m, p.Price);
        Assert.Equal(2000.00m, p.StoreOldPrice);
    }

    [Fact]
    public void MarkaVeSaticiAlanlariniDoldurur()
    {
        var p = ProteinimScraper.ParseProduct(Kayit(Urun))!;

        Assert.Equal("Olimp", p.BrandName);
        // BAYİ kaydı: Seller dolmazsa ürün markanın kendi sitesinden
        // geliyormuş gibi görünür.
        Assert.Equal("proteinim.com", p.Seller);
        Assert.True(p.InStock);
        Assert.Equal("https://proteinim.com/wp-content/uploads/olimp.jpg", p.ImageUrl);
    }

    [Fact]
    public void ZKonzeptTireliYazimaCevriliyor()
    {
        var p = ProteinimScraper.ParseProduct(Kayit("""
            {
              "name": "Z Konzept Iso Whey 900 Gr.",
              "permalink": "https://proteinim.com/urun/z-konzept-iso-whey/",
              "is_in_stock": true,
              "prices": { "price": "125000", "regular_price": "125000", "currency_minor_unit": 2 },
              "brands": [{ "name": "Z Konzept" }],
              "images": []
            }
            """))!;

        // Katalogda marka "Z-Konzept" olarak duruyor; eşlenmezse kopya
        // Brand kaydı oluşur.
        Assert.Equal("Z-Konzept", p.BrandName);
        // Kaynak bu üründe görsel vermiyor — 53 ürünün 13'ünde böyle.
        // Uydurma adres üretilmiyor.
        Assert.Null(p.ImageUrl);
    }

    [Fact]
    public void IndirimYoksaMagazaEskiFiyatiYazilmiyor()
    {
        var p = ProteinimScraper.ParseProduct(Kayit("""
            {
              "name": "Nutrend Creatine Monohydrate 500 Gr.",
              "permalink": "https://proteinim.com/urun/nutrend-creatine/",
              "is_in_stock": false,
              "prices": { "price": "99500", "regular_price": "99500", "currency_minor_unit": 2 },
              "brands": [{ "name": "Nutrend" }],
              "images": []
            }
            """))!;

        Assert.Equal(995.00m, p.Price);
        // regular == price: mağaza indirimi yok, uydurulmuyor.
        Assert.Null(p.StoreOldPrice);
        Assert.False(p.InStock);
    }

    [Fact]
    public void ShakerElenir()
    {
        var p = ProteinimScraper.ParseProduct(Kayit("""
            {
              "name": "Shaker / Şeffaf – 500 ML.",
              "permalink": "https://proteinim.com/urun/shaker-seffaf-500-ml/",
              "is_in_stock": true,
              "prices": { "price": "19500", "regular_price": "19500", "currency_minor_unit": 2 },
              "brands": [],
              "images": []
            }
            """));

        Assert.Null(p);
    }
}

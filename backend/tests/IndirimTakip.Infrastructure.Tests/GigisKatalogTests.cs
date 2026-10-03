using System.Net;
using System.Text;
using IndirimTakip.Infrastructure.Scraping.Gigis;

namespace IndirimTakip.Infrastructure.Tests;

// Kaynak 1 Ekim 2026'da ikas'tan Shopify'a taşındı. JSON canlı products.json
// yanıtından alınmış üç gerçek üründür (3 Ekim).
public class GigisKatalogTests
{
    private const string Katalog = """
        {"products":[
          {"title":"Gigi's Apple Pie Crunchie - Elmalı Tarçınlı Çıtır Kuruyemiş 115 g","handle":"crunchies-apple-pie",
           "product_type":"Atıştırmalık Kuruyemiş",
           "images":[{"src":"https://cdn.shopify.com/s/files/1/0709/0615/0979/files/image_1950_ef017bdc-3e76-4578-9679-215bf045f047.webp"}],
           "variants":[{"price":"419.00","compare_at_price":null,"available":true}]},
          {"title":"Gigi's Apple Pie El Yapımı Seramik Kase","handle":"el-yapimi-seramik-kase-apple-pie",
           "product_type":"Sunum & Tamamlayıcı","images":[],
           "variants":[{"price":"1500.00","compare_at_price":null,"available":true}]},
          {"title":"Gigi's Clutch Çanta","handle":"clutch-canta","product_type":"Sunum & Tamamlayıcı","images":[],
           "variants":[{"price":"500.00","compare_at_price":null,"available":true}]}
        ]}
        """;

    private static GigisScraper Scraper(string govde) =>
        new(new HttpClient(new SabitYanit(govde)) { BaseAddress = new Uri("https://gigis.com.tr/") });

    // Adres biçimi, ilk taramadan önce veritabanında güncellenen 37 kaydın
    // adresiyle birebir aynı olmalı; değişirse o kayıtlar ikinci kez kopyalanır.
    [Fact]
    public async Task Urun_adresi_tasima_bicimiyle_ayni_ve_aksesuarlar_eleniyor()
    {
        var urunler = await Scraper(Katalog).ScrapeAsync();

        var urun = Assert.Single(urunler);
        Assert.Equal("https://gigis.com.tr/products/crunchies-apple-pie", urun.Url);
        Assert.Equal(419m, urun.Price);
        Assert.Null(urun.StoreOldPrice);
        Assert.True(urun.InStock);
    }

    // Prime Nutrition'da site değişince tarama hata vermeden 0 ürün döndürmüştü.
    [Fact]
    public async Task Bos_katalog_hata_sayiliyor()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Scraper("""{"products":[]}""").ScrapeAsync());
    }

    private sealed class SabitYanit(string govde) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(govde, Encoding.UTF8, "application/json"),
            });
    }
}

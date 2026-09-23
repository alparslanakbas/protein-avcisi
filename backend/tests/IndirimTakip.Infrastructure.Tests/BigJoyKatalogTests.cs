using System.Net;
using System.Text;
using IndirimTakip.Infrastructure.Scraping.BigJoy;

namespace IndirimTakip.Infrastructure.Tests;

// Kaynak 22 Eylül'de yeniden yazıldı; katalog artık GET /api/products'tan
// geliyor. Buradaki JSON canlı yanıttan alınmış iki gerçek üründür.
public class BigJoyKatalogTests
{
    // Kreatin: tek KDV oranı (%1), varyant fiyatı KDV'siz (534,6535 -> 540).
    // Paket: tax_rate 0 YAZIYOR ama gerçek oran 2440/2330,78 = 1,0468 — sayfada
    // 1.634,80 TL görünüyor. İlk sürüm yalnızca tax_rate'e baktığı için bu ürünü
    // 1.561,62 TL gösterdi; katalogda 174 üründen 41'i bu durumda.
    private const string Katalog = """
        {"products":[
          {"name":"Bigjoy Creatine Monohydrate","seo_keyword":"creatine-255g","manufacturer_name":"Bigjoy",
           "tax_rate":1,"price":534.6535,"price_with_tax":540,"special":null,"special_with_tax":null,
           "is_in_stock":true,"thumb":"/image/creatine.png","category_ids":[1001,736],
           "variant_attributes":[
             {"seo_keyword":"creatine-255g","subgroup_value":"255g","price":534.6535,"special":null,"is_in_stock":true},
             {"seo_keyword":"creatine-510g","subgroup_value":"510g","price":990.099,"special":null,"is_in_stock":false}]},
          {"name":"Bigjoy Termojenik Paket","seo_keyword":"termojenik-paket","manufacturer_name":"Bigjoy",
           "tax_rate":0,"price":2330.78,"price_with_tax":2440,"special":1561.62,"special_with_tax":1634.8,
           "is_in_stock":true,"thumb":"/image/paket.png","category_ids":[878],
           "variant_attributes":[
             {"seo_keyword":"termojenik-paket","subgroup_value":null,"price":2330.78,"special":1561.62,"is_in_stock":true}]},
          {"name":"ONTHEGO Bar","seo_keyword":"onthego-bar","manufacturer_name":"ONTHEGO",
           "tax_rate":1,"price":100,"price_with_tax":101,"special":null,"special_with_tax":null,
           "is_in_stock":true,"thumb":"/image/bar.png","category_ids":[731],"variant_attributes":[]}
        ],"total":3,"hasMore":false}
        """;

    private static BigJoyScraper Scraper() =>
        new(new HttpClient(new KatalogHandler())
        {
            BaseAddress = new Uri("https://www.bigjoy.com.tr/"),
        });

    [Fact]
    public async Task Varyantlar_kendi_adresleriyle_ayri_satir_oluyor()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal(
            ["https://www.bigjoy.com.tr/creatine-255g", "https://www.bigjoy.com.tr/creatine-510g",
             "https://www.bigjoy.com.tr/termojenik-paket"],
            products.Select(p => p.Url).Order());
        Assert.Contains(products, p => p.Name == "Bigjoy Creatine Monohydrate 510g" && p.InStock == false);
    }

    // Vergi katsayısı ürünün KENDİ price/price_with_tax çiftinden ölçülüyor;
    // tax_rate'e güvenen sürüm paketi 1.561,62 TL gösteriyordu.
    [Fact]
    public async Task Karisik_kdvli_pakette_sitedeki_fiyat_yaziliyor()
    {
        var products = await Scraper().ScrapeAsync();

        var paket = Assert.Single(products, p => p.Url.EndsWith("/termojenik-paket"));
        Assert.Equal(1634.8m, paket.Price);
        Assert.Equal(2440m, paket.StoreOldPrice);
    }

    [Fact]
    public async Task Kdv_varyanta_da_uygulaniyor()
    {
        var products = await Scraper().ScrapeAsync();

        var tekli = Assert.Single(products, p => p.Url.EndsWith("/creatine-255g"));
        Assert.Equal(540m, tekli.Price);
        Assert.Null(tekli.StoreOldPrice);

        // 990,099 x 1,01 = 1000,00
        var buyuk = Assert.Single(products, p => p.Url.EndsWith("/creatine-510g"));
        Assert.Equal(1000m, buyuk.Price);
    }

    // BigJoy kendi sitesinde başka markaları da satıyor; onlar "BigJoy" markası
    // altında görünmemeli.
    [Fact]
    public async Task Baska_ureticiler_alinmiyor()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.DoesNotContain(products, p => p.Url.Contains("onthego"));
    }

    [Fact]
    public async Task Kategori_urunun_kendi_kimliklerinden_geliyor()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal("kreatin", Assert.Single(products, p => p.Url.EndsWith("/creatine-255g")).Category);
        // 878 (Avantajlı Paketler) bilerek eşlenmemiş: yanlış kategori, kategorisiz
        // kalmaktan kötü.
        Assert.Null(Assert.Single(products, p => p.Url.EndsWith("/termojenik-paket")).Category);
    }

    private sealed class KatalogHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Katalog, Encoding.UTF8, "application/json"),
            });
    }
}

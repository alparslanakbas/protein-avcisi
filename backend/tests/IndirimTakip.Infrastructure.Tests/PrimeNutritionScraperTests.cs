using System.Net;
using System.Text;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.PrimeNutrition;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndirimTakip.Infrastructure.Tests;

// Site 27 Eylül'de yenilendi; katalog artık GET /api/products'tan geliyor
// (BigJoy ile aynı altyapı). Buradaki JSON canlı yanıttan alınmış gerçek
// gruplardır, yalnızca kullanılmayan alanlar çıkarıldı.
public class PrimeNutritionScraperTests
{
    private const string Katalog = """
        {"products":[
          {"name":"Prime Nutrition Whey Protein","product_id":6188,"seo_keyword":"prime-nutrition-whey-protein-495-strawberry-6188","manufacturer_name":"Prime Nutrition",
           "tax_rate":1,"price":1286.14,"price_with_tax":1299,"special":null,"special_with_tax":null,"is_in_stock":true,
           "thumb":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Whey%20Kavanoz/495/15-Servis-Whey-Strawberry-Mockup-png-300x300.webp",
           "subgroup_value":"495 gram","variant_value":"Strawberry Cream","variant_attributes":[
             {"product_id":6188,"seo_keyword":"prime-nutrition-whey-protein-495-strawberry-6188","subgroup_value":"495 gram","variant_value":"Strawberry Cream","price":1286.14,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Whey%20Kavanoz/495/15-Servis-Whey-Strawberry-Mockup-png-300x300.webp"},
             {"product_id":6187,"seo_keyword":"whey-protein-double-chocolate-495g","subgroup_value":"495 gram","variant_value":"Double Chocolate","price":1286.14,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Whey%20Kavanoz/495/15-Servis-Whey-Chocolate-Mockup-png-yeni-300x300.webp"},
             {"product_id":6189,"seo_keyword":"whey-protein-cookie-ice-cream-495g","subgroup_value":"495 gram","variant_value":"Cookie & Ice Cream","price":1286.14,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Whey%20Kavanoz/495/15-Servis-Whey-Cookie-Mockup-png-300x300.webp"}]},
          {"name":"Prime Nutrition %100 Peanut Butter","product_id":6207,"seo_keyword":"100-peanut-butter","manufacturer_name":"Prime Nutrition",
           "tax_rate":1,"price":296.04,"price_with_tax":299,"special":null,"special_with_tax":null,"is_in_stock":true,
           "thumb":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Spread-Ezme/Peanut%20Butter/Peanut-Butter-Web1v-300x300.webp",
           "subgroup_value":"350 gram","variant_value":"none","variant_attributes":[
             {"product_id":6207,"seo_keyword":"100-peanut-butter","subgroup_value":"350 gram","variant_value":"none","price":296.04,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Spread-Ezme/Peanut%20Butter/Peanut-Butter-Web1v-300x300.webp"}]},
          {"name":"Prime Nutrition Optimus Pre-Workout","product_id":6170,"seo_keyword":"optimus-pre-workout-sachet-20x14g","manufacturer_name":"Prime Nutrition",
           "tax_rate":1,"price":890.1,"price_with_tax":899,"special":null,"special_with_tax":null,"is_in_stock":false,
           "thumb":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Optimus%20Display/Optimus-Display-Blue-Webpng-300x300.webp",
           "subgroup_value":"20 Adet x 14 gram","variant_value":"Blue Raspberry","variant_attributes":[
             {"product_id":6170,"seo_keyword":"optimus-pre-workout-sachet-20x14g","subgroup_value":"20 Adet x 14 gram","variant_value":"Blue Raspberry","price":890.1,"special":null,"is_in_stock":false,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Optimus%20Display/Optimus-Display-Blue-Webpng-300x300.webp"},
             {"product_id":6205,"seo_keyword":"optimus-pre-workout-sachet-redfruit-20x14g","subgroup_value":"20 Adet x 14 gram","variant_value":"Red Fruit","price":890.1,"special":null,"is_in_stock":false,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Optimus%20Display/Optimus%20Display%20K%C4%B1rm%C4%B1z%C4%B1%20Meyve%20Mockup%20png-300x300.webp"}]},
          {"name":"Prime Nutrition Beyaz 30x100 Siyah Havlu","product_id":6254,"seo_keyword":"beyaz-siyah-havlu","manufacturer_name":"Prime Nutrition",
           "tax_rate":10,"price":317.2727,"price_with_tax":349,"special":null,"special_with_tax":null,"is_in_stock":true,
           "thumb":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Aksesuar/Siyah%20Havlu/Prime-Havlu-Beyaz-Nak%C4%B1%C5%9F1-300x300.webp",
           "subgroup_value":null,"variant_value":null,"variant_attributes":[
             {"product_id":6254,"seo_keyword":"beyaz-siyah-havlu","subgroup_value":null,"variant_value":"none","price":317.2727,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Aksesuar/Siyah%20Havlu/Prime-Havlu-Beyaz-Nak%C4%B1%C5%9F1-300x300.webp"}]},
          {"name":"Jofit Straps","product_id":6142,"seo_keyword":"straps-siyah-mavi","manufacturer_name":"Jofit",
           "tax_rate":10,"price":117.27,"price_with_tax":129,"special":null,"special_with_tax":null,"is_in_stock":true,
           "thumb":"/image/cache/catalog/Jofit%20G%C3%BCncel/Lifting%20Straps/Mavi/Lifting-Straps-Mavi-5-300x300.webp",
           "subgroup_value":"Siyah & Mavi","variant_value":"Standart","variant_attributes":[
             {"product_id":6142,"seo_keyword":"straps-siyah-mavi","subgroup_value":"Siyah & Mavi","variant_value":"Standart","price":117.27,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Jofit%20G%C3%BCncel/Lifting%20Straps/Mavi/Lifting-Straps-Mavi-5-300x300.webp"}]},
          {"name":"Effive Nutrition ZMA 120 Kapsül","product_id":6230,"seo_keyword":"zma","manufacturer_name":"Effive Nutrition",
           "tax_rate":1,"price":513.86,"price_with_tax":519,"special":null,"special_with_tax":null,"is_in_stock":true,
           "thumb":"/image/cache/catalog/Effive%20Nutrition/Zma-1-300x300.webp",
           "subgroup_value":"120 Kapsül","variant_value":null,"variant_attributes":[
             {"product_id":6230,"seo_keyword":"zma","subgroup_value":"120 Kapsül","variant_value":"none","price":513.86,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Effive%20Nutrition/Zma-1-300x300.webp"}]},
          {"name":"Effive Nutrition Pillbox","product_id":6231,"seo_keyword":"pillbox","manufacturer_name":"Effive Nutrition",
           "tax_rate":20,"price":90.8333,"price_with_tax":109,"special":null,"special_with_tax":null,"is_in_stock":true,
           "thumb":"/image/cache/catalog/Effive%20Nutrition/Effive-pillbox-300x300.webp",
           "subgroup_value":"Siyah","variant_value":"Siyah","variant_attributes":[
             {"product_id":6231,"seo_keyword":"pillbox","subgroup_value":"Siyah","variant_value":"Siyah","price":90.8333,"special":null,"is_in_stock":true,"image":"/image/cache/catalog/Effive%20Nutrition/Effive-pillbox-300x300.webp"}]}
        ],"total":7,"hasMore":false}
        """;

    private static PrimeNutritionScraper Scraper(string json = Katalog) =>
        new(new HttpClient(new KatalogHandler(json))
        {
            BaseAddress = new Uri("https://www.primenutrition.com.tr/"),
        }, NullLogger<PrimeNutritionScraper>.Instance);

    // Ad, eski taramanın og:title'dan okuduğu adla birebir aynı olmalı: ad
    // değişirse sitemizdeki ürün adresi de değişir. Beklenen adlar canlı
    // veritabanındaki kayıtlardan.
    [Fact]
    public async Task Ad_grup_gramaj_ve_aromadan_eskisiyle_ayni_kuruluyor()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal("Prime Nutrition Whey Protein 495 gram Double Chocolate",
            Assert.Single(products, p => p.Url.EndsWith("/products/6187")).Name);
        Assert.Equal("Prime Nutrition Whey Protein 495 gram Cookie & Ice Cream",
            Assert.Single(products, p => p.Url.EndsWith("/products/6189")).Name);
    }

    // Aromasız üründe alan "none" diye DÜZ METİN geliyor.
    [Fact]
    public async Task None_aroma_ada_eklenmiyor()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal("Prime Nutrition %100 Peanut Butter 350 gram",
            Assert.Single(products, p => p.Url.EndsWith("/products/6207")).Name);
    }

    // Varyant fiyatı KDV'siz geliyor (1286,14); sitede ve eski taramada 1.299 TL.
    [Fact]
    public async Task Fiyat_kdvli_kart_fiyati()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal(1299m, Assert.Single(products, p => p.Url.EndsWith("/products/6187")).Price);
        Assert.Equal(299m, Assert.Single(products, p => p.Url.EndsWith("/products/6207")).Price);
        Assert.All(products, p => Assert.Null(p.StoreOldPrice));
    }

    [Fact]
    public async Task Her_varyant_kendi_adresiyle_ayri_satir()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal(
            ["https://www.primenutrition.com.tr/products/6170",
             "https://www.primenutrition.com.tr/products/6187",
             "https://www.primenutrition.com.tr/products/6188",
             "https://www.primenutrition.com.tr/products/6189",
             "https://www.primenutrition.com.tr/products/6205",
             "https://www.primenutrition.com.tr/products/6207",
             "https://www.primenutrition.com.tr/products/6230"],
            products.Select(p => p.Url).Order(StringComparer.Ordinal));
    }

    // REGRESYON (27 Eylül): mağaza iki tarama arasında 10 varyantın adres adını
    // değiştirdi ("...-495-strawberry-6188" -> "whey-protein-strawberry-495g")
    // ve adres adına bağlı sürüm her birine KOPYA kayıt açtı. Adres kimlikten
    // kurulduğu için ad değişse de aynı kalmalı.
    [Fact]
    public async Task Adres_adi_degisse_de_adres_ayni_kaliyor()
    {
        var yeniAdli = Katalog.Replace(
            "prime-nutrition-whey-protein-495-strawberry-6188", "whey-protein-strawberry-495g");
        Assert.NotEqual(Katalog, yeniAdli);

        var once = await Scraper().ScrapeAsync();
        var sonra = await Scraper(yeniAdli).ScrapeAsync();

        Assert.Equal(
            once.Select(p => p.Url).Order(StringComparer.Ordinal),
            sonra.Select(p => p.Url).Order(StringComparer.Ordinal));
        Assert.Single(sonra, p => p.Url.EndsWith("/products/6188"));
    }

    // Adres adına düşmek kopya kayıt sorununu geri getirirdi.
    [Fact]
    public async Task Kimliksiz_varyant_alinmiyor()
    {
        var kimliksiz = Katalog.Replace("\"product_id\":6187,", string.Empty);
        Assert.NotEqual(Katalog, kimliksiz);

        var products = await Scraper(kimliksiz).ScrapeAsync();

        Assert.DoesNotContain(products, p => p.Name == "Prime Nutrition Whey Protein 495 gram Double Chocolate");
        Assert.Equal(6, products.Count);
    }

    // Eski sayfa tükenen üründe fiyat yayınlamıyordu; uç fiyatı ve stoğu
    // birlikte veriyor.
    [Fact]
    public async Task Stokta_olmayan_varyant_tukendi_olarak_aliniyor()
    {
        var products = await Scraper().ScrapeAsync();

        var optimus = Assert.Single(products, p => p.Url.EndsWith("/products/6170"));
        Assert.False(optimus.InStock);
        Assert.Equal(899m, optimus.Price);
    }

    // Site Effive Nutrition'ı bayi olarak satıyor: marka Effive, satıcı bu site.
    [Fact]
    public async Task Effive_bayi_urunu_olarak_aliniyor()
    {
        var products = await Scraper().ScrapeAsync();

        var zma = Assert.Single(products, p => p.Url.EndsWith("/products/6230"));
        Assert.Equal("Effive Nutrition ZMA 120 Kapsül", zma.Name);
        Assert.Equal("Effive Nutrition", zma.BrandName);
        Assert.Equal("primenutrition.com.tr", zma.Seller);
        Assert.Equal(519m, zma.Price);
    }

    // Kendi ürünlerinde marka ve satıcı boş: ürün kaynağın kendi markasına yazılıyor.
    [Fact]
    public async Task Kendi_urunlerinde_marka_ve_satici_bos()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.All(products.Where(p => p.Name.StartsWith("Prime Nutrition")), p =>
        {
            Assert.Null(p.BrandName);
            Assert.Null(p.Seller);
        });
    }

    // Jofit listede olmayan üretici (aksesuarlarının bir kısmı süzgeçten
    // geçiyor); havlu ve pillbox ise aksesuar süzgecinde kalıyor.
    [Fact]
    public async Task Jofit_ve_aksesuarlar_alinmiyor()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.DoesNotContain(products, p => p.Url.EndsWith("/products/6142"));
        Assert.DoesNotContain(products, p => p.Url.EndsWith("/products/6254"));
        Assert.DoesNotContain(products, p => p.Url.EndsWith("/products/6231"));
    }

    [Fact]
    public async Task Gorsel_varyantin_kendi_resmi()
    {
        var products = await Scraper().ScrapeAsync();

        Assert.Equal(
            "https://www.primenutrition.com.tr/image/cache/catalog/Prime/Prime%20%C3%9Cr%C3%BCnler/Whey%20Kavanoz/495/15-Servis-Whey-Chocolate-Mockup-png-yeni-300x300.webp",
            Assert.Single(products, p => p.Url.EndsWith("/products/6187")).ImageUrl);
    }

    // Eski sürüm site değişince 0 ürünle sessizce "başarılı" döndü; bozulma
    // ancak 26 saat sonra sağlık ucunda görüldü.
    [Fact]
    public async Task Bos_katalog_hata_veriyor()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Scraper("""{"products":[],"total":0,"hasMore":false}""").ScrapeAsync());
    }

    // Sitenin kendi markalı aksesuarları (canlı katalogdaki adlar, 27 Eylül).
    // Üretici süzgecinden geçiyorlar; ortak süzgeç yakalamalı.
    [Theory]
    [InlineData("Prime Nutrition Beyaz 30x100 Siyah Havlu")]
    [InlineData("Prime Nutrition Yeşil 30x100 Siyah Havlu")]
    [InlineData("Prime Nutrition Kırmızı Optimus Anahtarlık Set")]
    [InlineData("Prime Nutrition Plaka Anahtarlık")]
    [InlineData("Prime Nutrition Premium Anahtarlık Set")]
    [InlineData("Prime Nutrition Matara 600 ml. Beyaz")]
    [InlineData("Prime Nutrition Premium Team Şapka Siyah Standart")]
    [InlineData("Prime Nutrition Soft-Tech Neon Sarı Tişört 2XLarge")]
    [InlineData("Prime Nutrition Soft-Tech Siyah Tişört Medium")]
    public void Markali_aksesuarlar_suzuluyor(string name)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(name));
    }

    [Theory]
    [InlineData("Prime Nutrition BCAA 2:1:1", "24 Adet x 10 gram", "Cool Lime", "Prime Nutrition BCAA 2:1:1 24 Adet x 10 gram Cool Lime")]
    [InlineData("Prime Nutrition Whey Protein", "66 Sachet x 33 gram", "Mix", "Prime Nutrition Whey Protein 66 Sachet x 33 gram Mix")]
    [InlineData("Prime Nutrition Shaker 400 ml.", "Pembe", "Pembe", "Prime Nutrition Shaker 400 ml. Pembe")]
    [InlineData("Prime Nutrition Smooth Coconut Spread", "350 gram", "None", "Prime Nutrition Smooth Coconut Spread 350 gram")]
    [InlineData("Prime Nutrition Whey Protein", "990 gram", "Cookie &amp; Ice Cream", "Prime Nutrition Whey Protein 990 gram Cookie & Ice Cream")]
    public void ComposeName_eski_ad_kuralini_veriyor(string group, string gramaj, string aroma, string expected)
    {
        Assert.Equal(expected, PrimeNutritionScraper.ComposeName(group, gramaj, aroma));
    }

    private sealed class KatalogHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}

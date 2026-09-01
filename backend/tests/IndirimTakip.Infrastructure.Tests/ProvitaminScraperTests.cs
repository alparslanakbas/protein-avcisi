using IndirimTakip.Infrastructure.Scraping.Provitamin;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndirimTakip.Infrastructure.Tests;

public class ProvitaminScraperTests
{
    [Fact]
    public void GercekWixUrunSemasiniOkur()
    {
        const string html = """
            <script type="application/ld+json">
            {"@context":"https://schema.org/","@type":"Product",
             "name":"Dymatize Nutrition Iso 100 Whey Protein Isolate 932 Gr Gourmet Chocolate",
             "brand":{"@type":"Brand","name":"Dymatize"},
             "image":[{"@type":"ImageObject","contentUrl":"https://static.wixstatic.com/iso100.jpg"}],
             "offers":{"@type":"Offer","priceCurrency":"TRY","price":"3850",
             "availability":"https://schema.org/OutOfStock"}}
            </script>
            """;

        var product = ProvitaminScraper.ParseProduct(html, "https://www.provitamin.com.tr/iso-100");

        Assert.NotNull(product);
        Assert.Equal("Dymatize", product.BrandName);
        Assert.Equal(3850m, product.Price);
        Assert.False(product.InStock);
        Assert.Equal("https://static.wixstatic.com/iso100.jpg", product.ImageUrl);
        Assert.Equal("provitamin.com.tr", product.Seller);
    }

    [Fact]
    public void SayisalFiyatVeStoktaUrunuOkur()
    {
        const string html = """
            <script type="application/ld+json">
            {"@type":["Thing","Product"],"name":"Big Joy Creatine 300 Gr",
             "brand":"Big Joy","image":"https://example.com/creatine.webp",
             "offers":[{"price":799.90,"availability":"https://schema.org/InStock"}]}
            </script>
            """;

        var product = ProvitaminScraper.ParseProduct(html, "https://www.provitamin.com.tr/creatine");

        Assert.NotNull(product);
        Assert.Equal("BigJoy", product.BrandName);
        Assert.Equal(799.90m, product.Price);
        Assert.True(product.InStock);
    }

    [Theory]
    [InlineData("Provitamin Shaker 700 ML", "Provitamin", "499")]
    [InlineData("Whey Protein 1000 Gr", "", "999")]
    [InlineData("Whey Protein 1000 Gr", "Provitamin", "0")]
    public void KapsamDisiVeyaGecersizUrunuAtlar(string name, string brand, string price)
    {
        var html = $$$"""
            <script type="application/ld+json">
            {"@type":"Product","name":"{{{name}}}","brand":{"name":"{{{brand}}}"},
             "offers":{"price":"{{{price}}}","availability":"https://schema.org/InStock"}}
            </script>
            """;

        Assert.Null(ProvitaminScraper.ParseProduct(html, "https://www.provitamin.com.tr/ornek"));
    }

    [Fact]
    public void StokBilgisiYoksaTahminEtmez()
    {
        const string html = """
            <script type="application/ld+json">
            {"@type":"Product","name":"Creatine Monohydrate 300 Gr",
             "brand":{"name":"Nutrend"},"offers":{"price":"799"}}
            </script>
            """;

        var product = ProvitaminScraper.ParseProduct(html, "https://www.provitamin.com.tr/creatine");

        Assert.NotNull(product);
        Assert.Null(product.InStock);
    }

    [Fact]
    public void GraphIcindeProductBulurVeBozukBloguAtlar()
    {
        const string html = """
            <script type="application/ld+json">{bozuk-json}</script>
            <script type="application/ld+json">
            {"@graph":[{"@type":"Organization","name":"Provitamin"},
             {"@type":"Product","name":"Trec BCAA 500 Gr","brand":{"name":"Trec Nutrition"},
              "offers":{"price":"1099","availability":"https://schema.org/InStock"}}]}
            </script>
            """;

        var product = ProvitaminScraper.ParseProduct(html, "https://www.provitamin.com.tr/bcaa");

        Assert.NotNull(product);
        Assert.Equal("Trec", product.BrandName);
    }

    [Fact]
    public void SitemapYalnizProvitaminAdresleriniTekillestirir()
    {
        const string xml = """
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://www.provitamin.com.tr/urun-1</loc></url>
              <url><loc>https://www.provitamin.com.tr/urun-1</loc></url>
              <url><loc>https://baska-site.example/urun</loc></url>
              <url><loc>http://www.provitamin.com.tr/guvensiz</loc></url>
            </urlset>
            """;

        var urls = ProvitaminScraper.ParseSitemapUrls(xml);

        Assert.Equal(["https://www.provitamin.com.tr/urun-1"], urls);
    }

    [Theory]
    [InlineData("Universal Nutrition", "Universal")]
    [InlineData("Proteinocean", "ProteinOcean")]
    [InlineData("Swiss", "Swiss Nutrition")]
    [InlineData("Nutrend", "Nutrend")]
    public void MarkaTakmaAdlariniMevcutKayitlarlaBirlestirir(string raw, string expected)
    {
        Assert.Equal(expected, ProvitaminScraper.NormalizeBrand(raw));
    }

    [Fact]
    public async Task HizSiniriGorulunceYeniIstekAtmadanDurur()
    {
        var handler = new RateLimitHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.provitamin.com.tr/"),
        };
        var scraper = new ProvitaminScraper(http, NullLogger<ProvitaminScraper>.Instance);

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => scraper.ScrapeAsync());

        Assert.Contains("429", error.Message);
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class RateLimitHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                const string sitemap = """
                    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                      <url><loc>https://www.provitamin.com.tr/urun-1</loc></url>
                    </urlset>
                    """;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(sitemap),
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests));
        }
    }

    [Theory]
    [InlineData("FITNESS PAKETİ - MEGA")]
    [InlineData("HACİM PAKETİ - LARGE")]
    [InlineData("fitness paketi - small")]
    public void CokUrunluSetlerAlinmaz(string ad)
    {
        // Tek fiyatı var ama içinde birden çok ürün: servis başı maliyet,
        // gramaj ve protein yoğunluğu anlamsız çıkıyor. Setin içeriği
        // değişince fiyat "düşmüş" görünüyor.
        Assert.True(ProvitaminScraper.IsBundle(ad));
    }

    [Theory]
    [InlineData("Dymatize Iso 100 Whey Protein 932 Gr")]
    [InlineData("Big Joy Creatine Monohydrate 300 Gr")]
    public void GercekUrunlerSetSanilmaz(string ad)
    {
        Assert.False(ProvitaminScraper.IsBundle(ad));
    }

    [Fact]
    public async Task HizSiniriGorulunceOnaKadarToplananlarKorunur()
    {
        // REGRESYON: eskiden ilk 429'da exception fırlatılıyordu ve o ana kadar
        // toplanan ürünler de kayboluyordu. 400. üründe gelen bir 429, 400
        // başarılı isteği ve o günün fiyat noktalarını çöpe atıyordu; bir
        // sonraki deneme ancak ertesi gece olduğu için fiyat geçmişinde tam bir
        // gün boşluk kalıyordu.
        var handler = new IkiUrunSonra429Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.provitamin.com.tr/") };
        var scraper = new ProvitaminScraper(http, NullLogger<ProvitaminScraper>.Instance);

        var products = await scraper.ScrapeAsync();

        Assert.Equal(2, products.Count);
        // 429'dan sonra YENİ istek atılmamalı: sitemap + 2 ürün + 429 = 4.
        Assert.Equal(4, handler.RequestCount);
    }

    private sealed class IkiUrunSonra429Handler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                const string sitemap = """
                    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                      <url><loc>https://www.provitamin.com.tr/urun-1</loc></url>
                      <url><loc>https://www.provitamin.com.tr/urun-2</loc></url>
                      <url><loc>https://www.provitamin.com.tr/urun-3</loc></url>
                      <url><loc>https://www.provitamin.com.tr/urun-4</loc></url>
                    </urlset>
                    """;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(sitemap),
                });
            }

            if (RequestCount <= 3)
            {
                // Ham dizede interpolasyon YOK: JSON'daki ardışık "}}" ile
                // $$ söz diziminin kapanış ayraçları çakışıyor.
                const string sablon = """
                    <script type="application/ld+json">
                    {"@context":"https://schema.org/","@type":"Product",
                     "name":"Test Whey Protein SIRA 1000 Gr",
                     "brand":{"@type":"Brand","name":"Dymatize"},
                     "offers":{"@type":"Offer","priceCurrency":"TRY","price":"1000",
                     "availability":"https://schema.org/InStock"}}
                    </script>
                    """;
                var html = sablon.Replace("SIRA", RequestCount.ToString());
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(html),
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests));
        }
    }
}

using System.Net;
using IndirimTakip.Infrastructure.Scraping;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>Shopify isteklerinde robots.txt'ye uyum (RFC 9309): bot kaydındaki sözün kendisi.</summary>
public class RobotsKurallariTests
{
    private const string Ad = RobotsTxtIsleyicisi.UrunAdi;

    // Shopify'ın varsayılan robots.txt'sinin biçimi (canlı bir mağazadan kısaltılmış, 8 Ekim 2026).
    private const string ShopifyVarsayilan = """
        User-agent: *
        Disallow: /admin
        Disallow: /cart
        Disallow: /checkout
        Disallow: /collections/*sort_by*
        Disallow: /search
        Sitemap: https://takehiq.com/sitemap.xml

        User-agent: adsbot-google
        Disallow: /checkout
        """;

    [Theory]
    [InlineData("/products.json?limit=250&page=1", true)]
    [InlineData("/", true)]
    [InlineData("/products/hiq-whey", true)]
    [InlineData("/admin", false)]
    [InlineData("/collections/all?sort_by=price", false)]
    public void Shopify_varsayilani_bizim_yollarimiza_izin_verir(string yol, bool izin) =>
        Assert.Equal(izin, RobotsKurallari.Ayristir(ShopifyVarsayilan, Ad).IzinVerir(yol));

    [Fact]
    public void Botumuzu_anan_grup_yildiz_grubunun_yerine_gecer()
    {
        var kurallar = RobotsKurallari.Ayristir("User-agent: *\nAllow: /\n\nUser-agent: WheyProofBot\nDisallow: /products.json\n", Ad);

        Assert.False(kurallar.IzinVerir("/products.json?limit=250"));
        Assert.True(kurallar.IzinVerir("/products/x"));
    }

    [Theory]
    [InlineData("/products.json", true)]
    [InlineData("/products/x", false)]
    public void En_uzun_eslesen_kural_kazanir(string yol, bool izin) =>
        Assert.Equal(izin, RobotsKurallari.Ayristir("User-agent: *\nDisallow: /products\nAllow: /products.json\n", Ad).IzinVerir(yol));

    [Theory]
    [InlineData("/products.json", false)]
    [InlineData("/products.json?limit=1", true)]
    public void Yildiz_ve_dolar_desenleri(string yol, bool izin) =>
        Assert.Equal(izin, RobotsKurallari.Ayristir("User-agent: *\nDisallow: /*.json$\n", Ad).IzinVerir(yol));

    // ---- işleyici --------------------------------------------------------------------------------

    private sealed class SahteZaman(DateTimeOffset simdi) : TimeProvider
    {
        public DateTimeOffset Simdi { get; set; } = simdi;
        public override DateTimeOffset GetUtcNow() => Simdi;
    }

    private sealed class Magaza(Func<HttpResponseMessage> robots) : HttpMessageHandler
    {
        public List<string> Yollar { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Yollar.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(request.RequestUri.AbsolutePath == "/robots.txt" ? robots() : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (HttpMessageInvoker Istemci, Magaza Magaza, SahteZaman Zaman) Kur(Func<HttpResponseMessage> robots,
        IReadOnlySet<string>? yalnizca = null)
    {
        var zaman = new SahteZaman(new DateTimeOffset(2026, 10, 8, 18, 0, 0, TimeSpan.Zero));
        var magaza = new Magaza(robots);
        var isleyici = new RobotsTxtIsleyicisi(new RobotsTxtOnbellegi(zaman), NullLogger<RobotsTxtIsleyicisi>.Instance, yalnizca)
        {
            InnerHandler = magaza,
        };
        return (new HttpMessageInvoker(isleyici), magaza, zaman);
    }

    private static HttpResponseMessage Robots(string metin) => new(HttpStatusCode.OK) { Content = new StringContent(metin) };

    private static Task<HttpResponseMessage> Iste(HttpMessageInvoker istemci, string adres) =>
        istemci.SendAsync(new HttpRequestMessage(HttpMethod.Get, adres), CancellationToken.None);

    [Fact]
    public async Task Izinli_istek_gider_ve_robots_gunde_bir_okunur()
    {
        var (istemci, magaza, zaman) = Kur(() => Robots(ShopifyVarsayilan));

        Assert.Equal(HttpStatusCode.OK, (await Iste(istemci, "https://takehiq.com/products.json?page=1")).StatusCode);
        zaman.Simdi += TimeSpan.FromHours(23);
        await Iste(istemci, "https://takehiq.com/products.json?page=2");
        Assert.Equal(1, magaza.Yollar.Count(y => y == "/robots.txt"));

        zaman.Simdi += TimeSpan.FromHours(2);
        await Iste(istemci, "https://takehiq.com/products.json?page=1");
        Assert.Equal(2, magaza.Yollar.Count(y => y == "/robots.txt"));
    }

    [Fact]
    public async Task Izin_verilmeyen_istek_gonderilmez()
    {
        var (istemci, magaza, _) = Kur(() => Robots("User-agent: wheyproofbot\nDisallow: /\n"));

        var yanit = await Iste(istemci, "https://takehiq.com/products.json");

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(RobotsTxtIsleyicisi.RetGerekcesi, yanit.ReasonPhrase);
        Assert.Equal(["/robots.txt"], magaza.Yollar);
    }

    [Fact]
    public async Task Robots_yoksa_her_sey_serbest()
    {
        var (istemci, _, _) = Kur(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal(HttpStatusCode.OK, (await Iste(istemci, "https://takehiq.com/products.json")).StatusCode);
    }

    [Fact]
    public async Task Ulasilamayan_robots_istegi_atlatir_ve_bir_saat_sonra_yeniden_sorulur()
    {
        var durum = HttpStatusCode.TooManyRequests;
        var (istemci, magaza, zaman) = Kur(() => durum == HttpStatusCode.OK ? Robots(ShopifyVarsayilan) : new HttpResponseMessage(durum));

        Assert.Equal(HttpStatusCode.Forbidden, (await Iste(istemci, "https://takehiq.com/products.json")).StatusCode);
        await Iste(istemci, "https://takehiq.com/products.json");
        Assert.Equal(["/robots.txt"], magaza.Yollar);

        durum = HttpStatusCode.OK;
        zaman.Simdi += RobotsTxtOnbellegi.UlasilamazsaTekrar;
        Assert.Equal(HttpStatusCode.OK, (await Iste(istemci, "https://takehiq.com/products.json")).StatusCode);
    }

    [Fact]
    public async Task Ulasilamayan_robots_son_kurallari_korur()
    {
        var durum = HttpStatusCode.OK;
        var (istemci, _, zaman) = Kur(() => durum == HttpStatusCode.OK ? Robots(ShopifyVarsayilan) : new HttpResponseMessage(durum));

        await Iste(istemci, "https://takehiq.com/products.json");
        durum = HttpStatusCode.ServiceUnavailable;
        zaman.Simdi += TimeSpan.FromHours(25);

        Assert.Equal(HttpStatusCode.OK, (await Iste(istemci, "https://takehiq.com/products.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Iste(istemci, "https://takehiq.com/admin")).StatusCode);
    }

    [Fact]
    public async Task Liste_disindaki_adreslere_bakilmaz()
    {
        var (istemci, magaza, _) = Kur(() => Robots("User-agent: *\nDisallow: /\n"), RobotsTxtIsleyicisi.ShopifyAdresleri);

        Assert.Equal(HttpStatusCode.OK, (await Iste(istemci, "https://www.ssnsports.com.tr/urun")).StatusCode);
        Assert.DoesNotContain("/robots.txt", magaza.Yollar);
    }

    // ---- Crawl-delay (Shopify'ın bot incelemesi nasıl uyduğumuzu sordu, 2 Kasım) ------------------

    [Theory]
    [InlineData("User-agent: *\nCrawl-delay: 10\n", 10.0)]
    [InlineData("User-agent: wheyproofbot\nCrawl-delay: 2.5\n\nUser-agent: *\nCrawl-delay: 30\n", 2.5)]
    [InlineData("User-agent: wheyproofbot\nDisallow: /cart\n\nUser-agent: *\nCrawl-delay: 30\n", null)]
    [InlineData("User-agent: *\nCrawl-delay: yakinda\n", null)]
    [InlineData("User-agent: *\nCrawl-delay: -5\n", null)]
    [InlineData("User-agent: *\nDisallow: /cart\n", null)]
    public void Crawl_delay_bizim_ya_da_yildiz_grubundan_geliyor(string metin, double? saniye) =>
        Assert.Equal(saniye is { } s ? TimeSpan.FromSeconds(s) : null, RobotsKurallari.Ayristir(metin, Ad).TaramaGecikmesi);

    [Fact]
    public async Task Crawl_delay_ayni_adrese_istekleri_araliyor()
    {
        var (istemci, magaza, _) = Kur(() => Robots("User-agent: *\nCrawl-delay: 0.4\n"));

        await Iste(istemci, "https://takehiq.com/products.json?page=1");
        var sayac = System.Diagnostics.Stopwatch.StartNew();
        await Iste(istemci, "https://takehiq.com/products.json?page=2");

        Assert.True(sayac.Elapsed >= TimeSpan.FromMilliseconds(350), $"yalnız {sayac.Elapsed} beklendi");
        Assert.Equal(["/robots.txt", "/products.json?page=1", "/products.json?page=2"], magaza.Yollar);
    }

    [Fact]
    public async Task Crawl_delay_gectiyse_beklenmiyor()
    {
        var (istemci, _, zaman) = Kur(() => Robots("User-agent: *\nCrawl-delay: 30\n"));

        await Iste(istemci, "https://takehiq.com/products.json?page=1");
        zaman.Simdi += TimeSpan.FromSeconds(31);
        var sayac = System.Diagnostics.Stopwatch.StartNew();
        await Iste(istemci, "https://takehiq.com/products.json?page=2");

        Assert.True(sayac.Elapsed < TimeSpan.FromSeconds(5), $"{sayac.Elapsed} beklendi");
    }

    [Fact]
    public async Task Bekledigimizden_uzun_crawl_delay_adresi_okutmuyor()
    {
        var (istemci, magaza, _) = Kur(() => Robots("User-agent: *\nCrawl-delay: 3600\n"));

        var yanit = await Iste(istemci, "https://takehiq.com/products.json?page=1");

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(RobotsTxtIsleyicisi.GecikmeGerekcesi, yanit.ReasonPhrase);
        Assert.Equal(["/robots.txt"], magaza.Yollar);
    }
}

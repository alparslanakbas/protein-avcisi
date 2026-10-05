using System.Net;
using IndirimTakip.Infrastructure.Scraping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Shopify 429 verince isteğin ev tünelinden tekrarlanması (bkz. ShopifyTuneli). En önemli
/// güvence: tünel kapalıyken ya da çalışmazken çekici tünelden önceki davranışı görüyor.
/// </summary>
public class ShopifyTuneliTests
{
    private const string Adres = "https://takehiq.com/products.json?limit=250&page=1";

    private sealed class SahteSaat(DateTimeOffset simdi) : TimeProvider
    {
        public DateTimeOffset Simdi { get; set; } = simdi;
        public override DateTimeOffset GetUtcNow() => Simdi;
    }

    /// <summary>Gelen istekleri sayan ve sırayla verilen yanıtları dönen sahte işleyici.</summary>
    private sealed class SahteIsleyici(Func<HttpRequestMessage, HttpResponseMessage> yanit) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Istekler { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Add(request);
            return Task.FromResult(yanit(request));
        }
    }

    private static HttpResponseMessage Yanit(HttpStatusCode kod) => new(kod);

    private static (HttpMessageInvoker Istemci, SahteIsleyici Dogrudan, SahteIsleyici? Tunel, SahteSaat Saat, ShopifyTuneli Durum)
        Kur(HttpStatusCode dogrudanKod, Func<HttpRequestMessage, HttpResponseMessage>? tunelYaniti, bool tunelVar = true)
    {
        var saat = new SahteSaat(new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.Zero));
        var dogrudan = new SahteIsleyici(_ => Yanit(dogrudanKod));
        var tunel = tunelVar ? new SahteIsleyici(tunelYaniti ?? (_ => Yanit(HttpStatusCode.OK))) : null;
        var durum = new ShopifyTuneli(tunel, saat);
        var isleyici = new ShopifyTunelIsleyicisi(durum, NullLogger<ShopifyTunelIsleyicisi>.Instance) { InnerHandler = dogrudan };
        return (new HttpMessageInvoker(isleyici), dogrudan, tunel, saat, durum);
    }

    private static Task<HttpResponseMessage> Gonder(HttpMessageInvoker istemci)
    {
        var istek = new HttpRequestMessage(HttpMethod.Get, Adres);
        istek.Headers.UserAgent.ParseAdd("Deneme/1.0");
        return istemci.SendAsync(istek, CancellationToken.None);
    }

    [Fact]
    public async Task Tunel_kapaliyken_429_cekiciye_aynen_gidiyor()
    {
        var (istemci, dogrudan, _, _, durum) = Kur(HttpStatusCode.TooManyRequests, null, tunelVar: false);

        var yanit = await Gonder(istemci);

        Assert.False(durum.Etkin);
        Assert.Equal(HttpStatusCode.TooManyRequests, yanit.StatusCode);
        Assert.Single(dogrudan.Istekler);
    }

    [Fact]
    public async Task Dogrudan_istek_basariliysa_tunel_kullanilmiyor()
    {
        var (istemci, dogrudan, tunel, _, durum) = Kur(HttpStatusCode.OK, null);

        var yanit = await Gonder(istemci);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Single(dogrudan.Istekler);
        Assert.Empty(tunel!.Istekler);
        Assert.False(durum.TuneldenGitmeli);
    }

    [Fact]
    public async Task Dogrudan_429_ayni_istekle_tunelden_tekrarlaniyor()
    {
        var (istemci, dogrudan, tunel, _, _) = Kur(HttpStatusCode.TooManyRequests, null);

        var yanit = await Gonder(istemci);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Single(dogrudan.Istekler);
        var tuneldeki = Assert.Single(tunel!.Istekler);
        Assert.Equal(Adres, tuneldeki.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, tuneldeki.Method);
        // Başlıklar kopyalanmalı: User-Agent'sız istekleri reddeden mağazalar var.
        Assert.Equal("Deneme/1.0", tuneldeki.Headers.UserAgent.ToString());
        // Aynı istek nesnesi iki kez gönderilemez; tünele kopyası gitmeli.
        Assert.NotSame(dogrudan.Istekler[0], tuneldeki);
    }

    [Fact]
    public async Task Engelden_sonra_yapiskan_sure_boyunca_dogrudan_denenmiyor_sonra_yine_deneniyor()
    {
        var (istemci, dogrudan, tunel, saat, _) = Kur(HttpStatusCode.TooManyRequests, null);

        await Gonder(istemci);                        // doğrudan 429 -> tünel
        saat.Simdi += TimeSpan.FromMinutes(29);
        await Gonder(istemci);                        // yapışkan: yalnız tünel

        Assert.Single(dogrudan.Istekler);
        Assert.Equal(2, tunel!.Istekler.Count);

        saat.Simdi += TimeSpan.FromMinutes(2);        // 31. dakika: süre doldu
        await Gonder(istemci);                        // önce doğrudan (429), sonra tünel

        Assert.Equal(2, dogrudan.Istekler.Count);
        Assert.Equal(3, tunel.Istekler.Count);
    }

    [Fact]
    public async Task Tunel_calismazsa_dogrudan_429_donuyor_ve_sonraki_istek_yine_dogrudan_deneniyor()
    {
        var (istemci, dogrudan, tunel, _, durum) = Kur(HttpStatusCode.TooManyRequests,
            _ => throw new HttpRequestException("Connection refused"));

        var yanit = await Gonder(istemci);

        Assert.Equal(HttpStatusCode.TooManyRequests, yanit.StatusCode);
        Assert.False(durum.TuneldenGitmeli);

        await Gonder(istemci);
        Assert.Equal(2, dogrudan.Istekler.Count);
        Assert.Equal(2, tunel!.Istekler.Count);
    }

    [Fact]
    public async Task Yapiskan_surede_tunel_duserse_istek_dogrudan_gidiyor()
    {
        var (istemci, dogrudan, _, _, durum) = Kur(HttpStatusCode.OK,
            _ => throw new HttpRequestException("Connection refused"));
        durum.EngelGoruldu();

        var yanit = await Gonder(istemci);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Single(dogrudan.Istekler);
    }

    [Fact]
    public async Task Tunelde_zaman_asimi_tunel_hatasi_ama_cagiranin_iptali_hata_olarak_yukari_cikiyor()
    {
        // Bağlantı zaman aşımı (iptal istenmeden gelen TaskCanceledException) = tünel çalışmadı.
        var (istemci, _, _, _, _) = Kur(HttpStatusCode.TooManyRequests, _ => throw new TaskCanceledException());
        var yanit = await Gonder(istemci);
        Assert.Equal(HttpStatusCode.TooManyRequests, yanit.StatusCode);

        // Tur iptal edildiyse (deploy, kapanış) yutulmamalı.
        using var iptal = new CancellationTokenSource();
        await iptal.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            istemci.SendAsync(new HttpRequestMessage(HttpMethod.Get, Adres), iptal.Token));
    }

    [Theory]
    [InlineData("http://172.17.0.1:8888")]
    [InlineData(" http://172.17.0.1:8888/ ")]
    public void Gecerli_vekil_adresi_kabul_ediliyor(string adres)
    {
        var uri = ShopifyTuneli.VekilAdresiniOku(adres);
        Assert.Equal("172.17.0.1", uri.Host);
        Assert.Equal(8888, uri.Port);
    }

    [Theory]
    [InlineData("172.17.0.1:8888")]               // şema yok
    [InlineData("https://172.17.0.1:8888")]       // vekile TLS'siz bağlanılıyor
    [InlineData("http://172.17.0.1:8888/yol")]
    [InlineData("http://172.17.0.1:8888/?a=1")]
    [InlineData("vekil")]
    public void Bozuk_vekil_adresi_sessizce_kapali_sayilmiyor(string adres)
    {
        Assert.Throws<InvalidOperationException>(() => ShopifyTuneli.VekilAdresiniOku(adres));
    }

    [Fact]
    public void Bos_ayar_tuneli_kapatiyor()
    {
        Assert.False(ShopifyTuneli.Olustur(null, TimeProvider.System).Etkin);
        Assert.False(ShopifyTuneli.Olustur(" ", TimeProvider.System).Etkin);
    }

    [Fact]
    public async Task Tunel_istemcisi_vekilden_baska_yere_baglanmiyor()
    {
        var vekil = new Uri("http://172.17.0.1:8888");

        Assert.True(ShopifyTuneli.VekilMi(new DnsEndPoint("172.17.0.1", 8888), vekil));
        Assert.False(ShopifyTuneli.VekilMi(new DnsEndPoint("172.17.0.1", 8080), vekil));
        Assert.False(ShopifyTuneli.VekilMi(new DnsEndPoint("localhost", 8888), vekil));
        Assert.False(ShopifyTuneli.VekilMi(new DnsEndPoint("takehiq.com", 443), vekil));

        // Ağa çıkmadan reddediliyor (WebProxy localhost'u vekilsiz gönderir; o yol bu kapıdan geçiyor).
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            ShopifyTuneli.VekileBaglanAsync(new DnsEndPoint("localhost", 80), vekil, CancellationToken.None).AsTask());

        using var isleyici = ShopifyTuneli.IsleyiciOlustur(vekil);
        Assert.True(isleyici.UseProxy);
        Assert.NotNull(isleyici.ConnectCallback);
    }

    // Üretim ayarındaki adres geçerli olmalı; bozuksa Shopify çekicileri her turda hata verirdi.
    [Fact]
    public void Uretim_ayarindaki_tunel_adresi_gecerli()
    {
        var ayarlar = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json")
            .Build();

        using var tunel = ShopifyTuneli.Olustur(ayarlar["Shopify:Tunel"], TimeProvider.System);

        Assert.True(tunel.Etkin);
    }

    // Tünel yalnızca Shopify çekicilerinde olmalı: başka bir kaynağın 429'unu (ör. Provitamin'in
    // gerçek hız sınırı) ev bağlantısından aşmak istemiyoruz. Kayıt eksikse (AddTransient
    // unutulursa) CreateHandler burada patlar.
    [Fact]
    public void Tunel_isleyicisi_tam_olarak_shopify_cekicilerinin_istemcilerinde()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var sp = services.BuildServiceProvider();
        var fabrika = sp.GetRequiredService<IHttpMessageHandlerFactory>();

        var adlar = sp.GetServices<IConfigureOptions<HttpClientFactoryOptions>>()
            .OfType<ConfigureNamedOptions<HttpClientFactoryOptions>>()
            .Select(o => o.Name)
            .Where(ad => !string.IsNullOrEmpty(ad))
            .Distinct();

        var tunelli = new List<string>();
        foreach (var ad in adlar)
        {
            for (var isleyici = fabrika.CreateHandler(ad!); isleyici is DelegatingHandler sarici; isleyici = sarici.InnerHandler!)
            {
                if (sarici is ShopifyTunelIsleyicisi)
                {
                    tunelli.Add(ad!);
                    break;
                }
            }
        }

        Assert.Equal(
            new[]
            {
                "BahsScraper", "CommanderNutritionScraper", "FellasScraper", "GigisScraper", "HiqScraper",
                "SupplementFactoryScraper", "SupraProteinScraper",
            },
            tunelli.Order(StringComparer.Ordinal).ToArray());
    }
}

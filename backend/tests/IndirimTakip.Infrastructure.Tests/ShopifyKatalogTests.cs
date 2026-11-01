using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.Bahs;
using IndirimTakip.Infrastructure.Scraping.CommanderNutrition;
using IndirimTakip.Infrastructure.Scraping.Fellas;
using IndirimTakip.Infrastructure.Scraping.Gigis;
using IndirimTakip.Infrastructure.Scraping.Hiq;
using IndirimTakip.Infrastructure.Scraping.SupplementFactory;
using IndirimTakip.Infrastructure.Scraping.SupraProtein;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Shopify kataloğu 429 verince mağazanın o turu hemen kaybetmemesi (bkz. ShopifyKatalog). Commander
/// 30 Ekim-1 Kasım'da 7 turun 5'inde bu yüzden atlandı.
/// </summary>
public class ShopifyKatalogTests
{
    private static readonly JsonSerializerOptions Secenekler = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>İstekleri sayan, sırayla verilen kodları dönen sahte işleyici; son kod tekrarlanıyor.</summary>
    private sealed class SahteIsleyici(params HttpStatusCode[] kodlar) : HttpMessageHandler
    {
        public int IstekSayisi { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var kod = kodlar[Math.Min(IstekSayisi, kodlar.Length - 1)];
            IstekSayisi++;
            return Task.FromResult(new HttpResponseMessage(kod)
            {
                Content = new StringContent("""{"products":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    private static HttpClient Istemci(HttpMessageHandler isleyici) => new(isleyici) { BaseAddress = new Uri("https://ornek.test/") };

    private static Task<ShopifyProductsResponse?> Getir(HttpClient istemci) =>
        ShopifyKatalog.SayfaGetirAsync(istemci, 1, Secenekler, "Deneme", null, CancellationToken.None, TimeSpan.Zero);

    [Fact]
    public async Task Sinir_yoksa_tek_istek_gidiyor()
    {
        var sahte = new SahteIsleyici(HttpStatusCode.OK);

        Assert.NotNull(await Getir(Istemci(sahte)));
        Assert.Equal(1, sahte.IstekSayisi);
    }

    [Fact]
    public async Task Ilk_429dan_sonra_ikinci_deneme_yaniti_veriyor()
    {
        var sahte = new SahteIsleyici(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        Assert.NotNull(await Getir(Istemci(sahte)));
        Assert.Equal(2, sahte.IstekSayisi);
    }

    [Fact]
    public async Task Ikinci_429_hata_olarak_cikiyor_ucuncu_istek_gitmiyor()
    {
        var sahte = new SahteIsleyici(HttpStatusCode.TooManyRequests);

        var hata = await Assert.ThrowsAsync<HttpRequestException>(() => Getir(Istemci(sahte)));

        Assert.Equal(HttpStatusCode.TooManyRequests, hata.StatusCode);
        Assert.Equal(2, sahte.IstekSayisi);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Baska_hata_tekrar_denenmiyor(HttpStatusCode kod)
    {
        var sahte = new SahteIsleyici(kod);

        await Assert.ThrowsAsync<HttpRequestException>(() => Getir(Istemci(sahte)));
        Assert.Equal(1, sahte.IstekSayisi);
    }

    [Fact]
    public void Uretimdeki_bekleme_bir_dakika()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), ShopifyKatalog.VarsayilanBekleme);
    }

    public static TheoryData<string, Func<HttpClient, IBrandScraper>> Cekiciler => new()
    {
        { "Bahs", c => new BahsScraper(c) },
        { "Commander", c => new CommanderNutritionScraper(c) },
        { "Fellas", c => new FellasScraper(c) },
        { "Gigi's", c => new GigisScraper(c) },
        { "HIQ", c => new HiqScraper(c) },
        { "Supplement Factory", c => new SupplementFactoryScraper(c) },
        { "Supra", c => new SupraProteinScraper(c) },
    };

    // Çekici yardımcıyı kullanıyorsa ilk 429'da beklemeye geçer ve iptalle düşer; kullanmıyorsa hemen
    // HttpRequestException atar. Bekleme bitmeden ikinci istek gitmemeli.
    [Theory]
    [MemberData(nameof(Cekiciler))]
    public async Task Yedi_cekici_de_429da_hata_atmadan_bekliyor(string ad, Func<HttpClient, IBrandScraper> olustur)
    {
        var sahte = new SahteIsleyici(HttpStatusCode.TooManyRequests);
        using var iptal = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => olustur(Istemci(sahte)).ScrapeAsync(iptal.Token));
        Assert.True(sahte.IstekSayisi == 1, $"{ad}: bekleme bitmeden ikinci istek gitti ({sahte.IstekSayisi})");
    }

    // Yeni kurucu parametresi (günlükçü) typed client etkinleştirmesini bozmamalı.
    [Fact]
    public void Cekiciler_uygulama_kaydindan_olusuyor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var sp = services.BuildServiceProvider();
        using var kapsam = sp.CreateScope();

        Assert.NotNull(kapsam.ServiceProvider.GetRequiredService<CommanderNutritionScraper>());
        Assert.NotNull(kapsam.ServiceProvider.GetRequiredService<HiqScraper>());
        Assert.NotNull(kapsam.ServiceProvider.GetRequiredService<BahsScraper>());
    }
}

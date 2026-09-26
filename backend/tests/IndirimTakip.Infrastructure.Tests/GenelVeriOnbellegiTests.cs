using System.Collections.Concurrent;
using IndirimTakip.Api.Caching;
using IndirimTakip.Api.Endpoints;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Genel veri önbelleğinin anahtarı (güvenlik incelemesi, 26 Eylül): rastgele
/// bir sorgu parametresi önbelleği atlatmamalı, ucun GERÇEKTEN okuduğu
/// parametre ise ayrı girdi açmaya devam etmeli — ikincisi bozulursa
/// <c>page=2</c> isteği <c>page=1</c>'in yanıtını alır.
/// Politika Program.cs'teki kurulumun AYNISIYLA (<see cref="GenelVeriOnbellegi"/>)
/// gerçek bir istek hattında çalıştırılıyor.
/// </summary>
public class GenelVeriOnbellegiTests
{
    private sealed class Sunucu(WebApplication app, ConcurrentDictionary<string, int> cagrilar) : IAsyncDisposable
    {
        public HttpClient Istemci { get; } = app.GetTestClient();

        public int Cagri(string uc) => cagrilar.GetValueOrDefault(uc);

        public async Task GetAsync(string adres) => (await Istemci.GetAsync(adres)).EnsureSuccessStatusCode();

        public async ValueTask DisposeAsync() => await app.DisposeAsync();
    }

    private static async Task<Sunucu> KurAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOutputCache(o =>
        {
            o.AddBasePolicy(p => p.NoCache());
            o.AddPolicy("genel", p => GenelVeriOnbellegi.Uygula(p, TimeSpan.FromMinutes(5)));
        });
        var app = builder.Build();
        app.UseOutputCache();

        var cagrilar = new ConcurrentDictionary<string, int>();
        int Say(string uc) => cagrilar.AddOrUpdate(uc, 1, (_, n) => n + 1);

        app.MapGet("/parametresiz", () => Say("parametresiz")).CacheOutput("genel");
        app.MapGet("/liste", (int? page, string[]? brands) => Say("liste")).CacheOutput("genel");
        app.MapGet("/urun/{id:int}", (int id, int? days) => Say("urun")).CacheOutput("genel");
        app.MapGet("/ham", (HttpContext ctx) => Say("ham")).CacheOutput("genel");

        await app.StartAsync();
        return new Sunucu(app, cagrilar);
    }

    [Fact]
    public async Task Parametresiz_uca_eklenen_rastgele_parametre_ayni_girdiye_duser()
    {
        await using var s = await KurAsync();

        await s.GetAsync("/parametresiz?r=1");
        await s.GetAsync("/parametresiz?r=2");
        await s.GetAsync("/parametresiz");

        Assert.Equal(1, s.Cagri("parametresiz"));
    }

    [Fact]
    public async Task Ucun_okudugu_parametre_ayri_girdi_acar_ilgisiz_parametre_acmaz()
    {
        await using var s = await KurAsync();

        await s.GetAsync("/liste?page=1");
        await s.GetAsync("/liste?page=2");
        Assert.Equal(2, s.Cagri("liste"));

        await s.GetAsync("/liste?page=1&r=9");
        await s.GetAsync("/liste?PAGE=2");
        Assert.Equal(2, s.Cagri("liste"));

        await s.GetAsync("/liste?page=1&brands=a&brands=b");
        Assert.Equal(3, s.Cagri("liste"));
        await s.GetAsync("/liste?brands=a&brands=b&page=1&utm_source=x");
        Assert.Equal(3, s.Cagri("liste"));
    }

    [Fact]
    public async Task Rota_parametresi_yolda_kalir_sorgudaki_ilgisiz_parametre_dusmez()
    {
        await using var s = await KurAsync();

        await s.GetAsync("/urun/1?days=7");
        await s.GetAsync("/urun/1?days=7&r=1");
        Assert.Equal(1, s.Cagri("urun"));

        await s.GetAsync("/urun/2?days=7");
        await s.GetAsync("/urun/1?days=30");
        Assert.Equal(3, s.Cagri("urun"));
    }

    [Fact]
    public async Task Ham_istegi_okuyan_uc_eski_davranista_kalir()
    {
        // HttpContext alan uç sorguyu kendisi okuyabilir; anahtar daraltılırsa
        // farklı istekler aynı yanıtı alırdı. Burada bütün parametreler anahtarda.
        await using var s = await KurAsync();

        await s.GetAsync("/ham?x=1");
        await s.GetAsync("/ham?x=2");

        Assert.Equal(2, s.Cagri("ham"));
    }

    [Fact]
    public void Gercek_uclarin_anahtarlari_imzalarindaki_parametrelerle_ayni()
    {
        // Sitenin gerçek uç tanımları kuruluyor; servis parametreleri "servis"
        // diye tanınsın diye tipler kayıtlı (hiçbiri çözülmüyor, DB gerekmiyor).
        // Uç listesini kurmak minimal API çıkarımını da çalıştırıyor: 6 Eylül'de
        // tam bu aşamada atılan hata API'nin her ucunu 500'e düşürmüştü.
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<DealsQueryService>();
        builder.Services.AddScoped<CatalogStatsQueryService>();
        builder.Services.AddScoped<PriceHistoryQueryService>();
        builder.Services.AddScoped<ArticleService>();
        builder.Services.AddScoped<CouponService>();
        var app = builder.Build();
        app.MapDealsEndpoints("genel");
        app.MapCouponEndpoints("genel");
        app.MapArticleEndpoints("genel");
        app.MapPriceHistoryEndpoints("genel");

        var gercek = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(u => $"{u.RoutePattern.RawText} -> {string.Join(",", UcSorguAnahtarlariPolitikasi.Anahtarlar(u).ToArray())}")
            .Order(StringComparer.Ordinal)
            .ToList();

        const string liste = "brands,categories,sellers,search,minPrice,maxPrice,sortBy,page,pageSize,expandSynonyms,preferBrandStore";
        string[] beklenen =
        [
            "/api/articles -> ",
            "/api/articles/{slug} -> ",
            "/api/best-value-brands -> category",
            "/api/best-value-per-serving -> category,brands,search,page,pageSize",
            "/api/brand-category-pairs -> ",
            "/api/brand-comparison -> brand1,brand2",
            "/api/brand-product-counts -> ",
            "/api/brand-stats -> brand,category",
            "/api/category-price-stats -> category",
            "/api/category-product-counts -> ",
            "/api/coupons -> ",
            $"/api/deals -> {liste}",
            "/api/filters -> ",
            "/api/preferred-products -> count",
            $"/api/products -> {liste}",
            "/api/products/sitemap -> ",
            "/api/products/sparklines -> ids,days",
            "/api/products/{id:int} -> ",
            "/api/products/{id:int}/price-history -> days",
            "/api/stats -> ",
            $"/api/store-deals -> {liste}",
        ];
        Assert.Equal(beklenen.Order(StringComparer.Ordinal), gercek);
    }
}

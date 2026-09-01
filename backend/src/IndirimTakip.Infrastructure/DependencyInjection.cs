using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.Hardline;
using IndirimTakip.Infrastructure.Scraping.Hiq;
using IndirimTakip.Infrastructure.Scraping.BigJoy;
using IndirimTakip.Infrastructure.Scraping.CommanderNutrition;
using IndirimTakip.Infrastructure.Scraping.Gnc;
using IndirimTakip.Infrastructure.Scraping.Heyday;
using IndirimTakip.Infrastructure.Scraping.PrimeNutrition;
using IndirimTakip.Infrastructure.Scraping.Protein7;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;
using IndirimTakip.Infrastructure.Scraping.Provitamin;
using IndirimTakip.Infrastructure.Scraping.Ssn;
using IndirimTakip.Infrastructure.Scraping.SpaceSupplements;
using IndirimTakip.Infrastructure.Scraping.SupraProtein;
using IndirimTakip.Infrastructure.Scraping.Supplementler;
using IndirimTakip.Infrastructure.Scraping.Torq;
using IndirimTakip.Infrastructure.Scraping.West;
using IndirimTakip.Infrastructure.Scraping.SwissNutrition;
using IndirimTakip.Infrastructure.Scraping.Yesilmarka;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IndirimTakip.Infrastructure;

public static class DependencyInjection
{
    // Bazı siteler (Cloudflare arkasındakiler dahil) User-Agent'sız istekleri engelliyor.
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddHttpClient<HiqScraper>(client =>
        {
            client.BaseAddress = new Uri("https://takehiq.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<HiqScraper>());

        // Commander Nutrition — HIQ ile aynı Shopify products.json deseni.
        // VM'den erişim test edildi (200), Cloudflare engeli yok.
        services.AddHttpClient<CommanderNutritionScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.commandernutrition.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<CommanderNutritionScraper>());

        // Supra Protein — Commander/HIQ ile aynı Shopify products.json deseni.
        services.AddHttpClient<SupraProteinScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.supraprotein.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SupraProteinScraper>());

        // Space Supplements — custom Laravel mağaza; sitemap + ürün
        // sayfasındaki schema.org Product verisi, toplam yedi adres.
        services.AddHttpClient<SpaceSupplementsScraper>(client =>
        {
            client.BaseAddress = new Uri("https://spacegymsupplements.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SpaceSupplementsScraper>());

        // protein7 — BAYİ (çok markalı) kaynak. Ürün başına bir istek attığı
        // için DailyOnly: 6 saatlik genel tura değil, günde bir kez çalışan
        // tura giriyor (bkz. DailyScrapingBackgroundService).
        services.AddHttpClient<Protein7Scraper>(client =>
        {
            client.BaseAddress = new Uri("https://protein7.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            // ~900 ürün sayfası tek tek geziliyor; tek sayfa için varsayılan
            // timeout yeterli ama yavaş yanıtlarda takılıp kalmasın.
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<Protein7Scraper>());

        // Provitamin — Wix tabanlı çok markalı bayi. Ürün detayları sitemap
        // adreslerinden tek tek okunduğu için günde bir çalışan kaynak.
        services.AddHttpClient<ProvitaminScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.provitamin.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<ProvitaminScraper>());

        services.AddHttpClient<SsnScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.ssnsports.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SsnScraper>());

        services.AddHttpClient<TorqScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.torqnutrition.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<TorqScraper>());

        services.AddHttpClient<WestNutritionScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.westnutrition.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<WestNutritionScraper>());

        services.AddHttpClient<HardlineScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.hardlinenutrition.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<HardlineScraper>());

        services.AddHttpClient<ProteinOceanScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<ProteinOceanScraper>());

        // GNC Türkiye — ProteinOcean/Yeşilmarka ile aynı ikas storefront API'si.
        services.AddHttpClient<GncScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<GncScraper>());

        // Heyday — GNC ile aynı ikas storefront API'si, farkı yalnızca kimlikler.
        services.AddHttpClient<HeydayScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<HeydayScraper>());

        // Prime Nutrition — OpenCart; ürün başına HTML isteği (bkz. scraper
        // yorumu: sitenin schema.org fiyatı bozuk).
        services.AddHttpClient<PrimeNutritionScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.primenutrition.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<PrimeNutritionScraper>());

        services.AddHttpClient<YesilmarkaScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<YesilmarkaScraper>());

        services.AddHttpClient<SwissNutritionScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SwissNutritionScraper>());

        services.AddHttpClient<BigJoyScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.bigjoy.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<BigJoyScraper>());

        // Supplementler DEVRE DIŞI — kod hazır ve doğrulandı (545 ürün, 29
        // marka) ama sunucudan siteye erişilemiyor: site Cloudflare bot
        // koruması arkasında ve veri merkezi IP'lerini 403 ile engelliyor
        // (ana sayfa dahil her istek). Geliştirme makinesinden çalışıyor,
        // üretimden çalışmıyor. Bot korumasını aşmak seçenek değil; doğru
        // yol siteden izin/veri akışı istemek. İzin gelirse aşağıdaki iki
        // satırı geri açmak yeterli.
        //
        // services.AddHttpClient<SupplementlerScraper>(client =>
        // {
        //     client.BaseAddress = new Uri("https://www.supplementler.com/");
        //     client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        // });
        // services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SupplementlerScraper>());

        // Arama motorlarına sayfa değişikliği bildirimi (Bing/Yandex/Seznam).
        services.AddHttpClient<IndexNowClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // Puan tazeleme, markaya özel bir scraper'a bağlı değil: tüm markalar
        // puanı ürün sayfasında aynı schema.org alanlarıyla verdiği için tek
        // bir genel istemci yetiyor (bkz. ProductRatingRefreshService).
        services.AddHttpClient(ProductRatingRefreshService.RatingHttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<ProductRatingRefreshService>();

        services.AddScoped<ScrapeIngestionService>();
        services.AddScoped<ProductDetailBackfillService>();
        services.AddScoped<DealsQueryService>();
        services.AddScoped<PriceHistoryQueryService>();
        services.AddScoped<CouponService>();
        services.AddScoped<ArticleService>();
        services.AddHostedService<ScrapingBackgroundService>();
        // Günde bir kez, 00:00 Türkiye saatinde çalışan kaynaklar (bkz. IBrandScraper.DailyOnly).
        services.AddHostedService<DailyScrapingBackgroundService>();
        services.AddHostedService<DescriptionBackfillBackgroundService>();
        services.AddHostedService<RatingRefreshBackgroundService>();

        services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/");
        });
        services.AddScoped<EmailAddressValidator>();
        services.AddScoped<SubscriberService>();
        services.AddScoped<DigestService>();
        services.AddScoped<ProductWatchService>();
        services.AddScoped<ProductWatchNotifier>();
        services.AddScoped<FavoriteService>();
        services.AddHostedService<DigestBackgroundService>();

        return services;
    }
}

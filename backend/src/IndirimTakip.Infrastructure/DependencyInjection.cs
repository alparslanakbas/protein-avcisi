using IndirimTakip.Core.Caching;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.Hardline;
using IndirimTakip.Infrastructure.Scraping.Hiq;
using IndirimTakip.Infrastructure.Scraping.BigJoy;
using IndirimTakip.Infrastructure.Scraping.Biofitle;
using IndirimTakip.Infrastructure.Scraping.CommanderNutrition;
using IndirimTakip.Infrastructure.Scraping.DrSupplement;
using IndirimTakip.Infrastructure.Scraping.FitCarsi;
using IndirimTakip.Infrastructure.Scraping.Gigis;
using IndirimTakip.Infrastructure.Scraping.Gnc;
using IndirimTakip.Infrastructure.Scraping.Grizzone;
using IndirimTakip.Infrastructure.Scraping.Heyday;
using IndirimTakip.Infrastructure.Scraping.ImperiumSupplements;
using IndirimTakip.Infrastructure.Scraping.Kiperin;
using IndirimTakip.Infrastructure.Scraping.MlaProtein;
using IndirimTakip.Infrastructure.Scraping.MusclePump;
using IndirimTakip.Infrastructure.Scraping.Nois;
using IndirimTakip.Infrastructure.Scraping.PrimeNutrition;
using IndirimTakip.Infrastructure.Scraping.Protein34;
using IndirimTakip.Infrastructure.Scraping.Protein7;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;
using IndirimTakip.Infrastructure.Scraping.Provitamin;
using IndirimTakip.Infrastructure.Scraping.Renovafood;
using IndirimTakip.Infrastructure.Scraping.S4u;
using IndirimTakip.Infrastructure.Scraping.Ssn;
using IndirimTakip.Infrastructure.Scraping.SpaceSupplements;
using IndirimTakip.Infrastructure.Scraping.SupplementFactory;
using IndirimTakip.Infrastructure.Scraping.SupraProtein;
using IndirimTakip.Infrastructure.Scraping.Supplementler;
using IndirimTakip.Infrastructure.Scraping.ThinkNutrition;
using IndirimTakip.Infrastructure.Scraping.Torq;
using IndirimTakip.Infrastructure.Scraping.West;
using IndirimTakip.Infrastructure.Scraping.SwissNutrition;
using IndirimTakip.Infrastructure.Scraping.Vitabear;
using IndirimTakip.Infrastructure.Scraping.Yesilmarka;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Tarama bitince genel veri önbelleğini tazeleyen bağımlılık. GERÇEK
        // uygulama Api projesinde (ASP.NET'in çıktı önbelleğine bağlı);
        // buradaki yalnızca Api olmadan çalışan ortamlar (testler, konsol
        // araçları) için boş yedek. TryAdd olduğu için Api'nin kaydını EZMEZ.
        services.TryAddScoped<IPublicCacheRefresher, NullPublicCacheRefresher>();

        // Fiyat özeti (Products üzerindeki önceden hesaplanmış alanlar) her
        // taramadan sonra tek küme sorgusuyla tazeleniyor.
        services.AddScoped<PriceSummaryRefresher>();

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

        // Supplement Factory — Supra/Commander ile aynı Shopify deseni.
        services.AddHttpClient<SupplementFactoryScraper>(client =>
        {
            client.BaseAddress = new Uri("https://supplementfactory.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SupplementFactoryScraper>());

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

        // Fit Çarşı — ÜÇÜNCÜ BAYİ, özel ASP.NET mağazası. Ürün başına istek
        // ATMIYOR (25 marka sayfası yetiyor), o yüzden DailyOnly değil.
        services.AddHttpClient<FitCarsiScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.fitcarsi.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<FitCarsiScraper>());

        services.AddHttpClient<SsnScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.ssnsports.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SsnScraper>());

        // S4U — OpenCart, ürün sayfasındaki schema.org bloğu. Marka katalogda
        // ZATEN var (protein7 üzerinden); ad birebir aynı olduğu için aynı
        // Brand kaydına düşüyor, Seller null kaldığından bayi kayıtlarından
        // ayrılıyor.
        services.AddHttpClient<S4uScraper>(client =>
        {
            client.BaseAddress = new Uri("https://s4u.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<S4uScraper>());

        // Vitabear — Laravel; katalogun tamamı TEK istekle JSON olarak
        // geliyor (`/products/get?cat=all`). Çerez/CSRF gerekmiyor.
        // Marka katalogda YOKTU (3 Eylül'de canlı marka listesiyle
        // doğrulandı), yani kopya Brand riski yok.
        services.AddHttpClient<VitabearScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.vitabear.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<VitabearScraper>());

        // Gigi's — ikas; products.xml sitemap + ürün sayfasındaki schema.org.
        // GraphQL ucu bu mağazada totalCount:0 döndüğü için sitemap yolu
        // kullanılıyor (bkz. IkasSchemaOrgCatalog).
        services.AddHttpClient<GigisScraper>(client =>
        {
            client.BaseAddress = new Uri("https://gigis.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<GigisScraper>());

        // MLA Protein — Gigi's ile aynı desen, farkı ÇOK MARKALI olması:
        // marka ürün sayfasından okunuyor (Nutraxin, Dr. Pan, FitNut,
        // Seedn Grains de satıyor).
        services.AddHttpClient<MlaProteinScraper>(client =>
        {
            client.BaseAddress = new Uri("https://mlaprotein.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<MlaProteinScraper>());

        // protein34 — DÖRDÜNCÜ BAYİ, IdeaSoft. Marka ürün sayfasından
        // okunuyor; taşıdığı 14 markanın hepsi katalogda zaten var, yeni
        // üretici getirmiyor. Değeri aynı ürün için ek bir fiyat noktası.
        services.AddHttpClient<Protein34Scraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.protein34.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<Protein34Scraper>());

        // Grizzone / Kiperin / Renovafood — üçü de "sitemap + ürün sayfasındaki
        // schema.org" deseni. İlk ikisi ikas, Renovafood Ticimax; desen
        // platformdan bağımsız çalıştığı için aynı taban sınıfı paylaşıyorlar.
        services.AddHttpClient<GrizzoneScraper>(client =>
        {
            client.BaseAddress = new Uri("https://grizzone.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<GrizzoneScraper>());

        services.AddHttpClient<KiperinScraper>(client =>
        {
            client.BaseAddress = new Uri("https://kiperinturkiye.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<KiperinScraper>());

        // RENOVAFOOD DEVRE DIŞI — site sunucumuzun IP'sini ENGELLİYOR.
        //
        // 3 Eylül'de deploy sonrası ölçüldü: sitemap VM'den 200 dönüyor ama
        // ÜRÜN SAYFALARI 403 (User-Agent'lı da, UA'sız da). Geliştirme
        // makinesinden 38/38 ürün sorunsuz alınıyordu — yani kod doğru, engel
        // ağ tarafında. Tarama turunda "38 adresin 38'inde hata" ile
        // düşüyordu.
        //
        // Supplementler.com ile aynı durum. Kod duruyor; site IP izni verirse
        // ya da engel kalkarsa aşağıdaki iki satırı açmak yeterli.
        //
        // services.AddHttpClient<RenovafoodScraper>(client =>
        // {
        //     client.BaseAddress = new Uri("https://renovafood.com.tr/");
        //     client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        //     client.Timeout = TimeSpan.FromSeconds(30);
        // });
        // services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<RenovafoodScraper>());

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

        // Nois Nutrition — İkas public storefront GraphQL kataloğu.
        services.AddHttpClient<NoisScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<NoisScraper>());

        // Dr Supplement — İkas public storefront GraphQL kataloğu.
        services.AddHttpClient<DrSupplementScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<DrSupplementScraper>());

        // Biofitle — İkas public storefront GraphQL kataloğu; yalnız açıkça
        // yüksek proteinli kahvaltılık gevrekler kapsamda.
        services.AddHttpClient<BiofitleScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<BiofitleScraper>());

        // Muscle Pump — AKINSOFT public ürün sitemap'i ve ürün HTML'i.
        // Fitness aksesuarları ve kombinasyon altındaki standlar sitemap
        // aşamasında elenir.
        services.AddHttpClient<MusclePumpScraper>(client =>
        {
            client.BaseAddress = new Uri("https://musclepump.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<MusclePumpScraper>());

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

        // Think Nutrition — GNC/Heyday ile aynı ikas storefront API'si.
        services.AddHttpClient<ThinkNutritionScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<ThinkNutritionScraper>());

        // Imperium Supplements — GNC/Heyday ile aynı ikas storefront API'si.
        services.AddHttpClient<ImperiumSupplementsScraper>(client =>
        {
            client.BaseAddress = new Uri("https://api.myikas.com/");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<ImperiumSupplementsScraper>());

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

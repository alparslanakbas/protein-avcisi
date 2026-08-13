using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.Hardline;
using IndirimTakip.Infrastructure.Scraping.Hiq;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;
using IndirimTakip.Infrastructure.Scraping.Ssn;
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

        services.AddHttpClient<SsnScraper>(client =>
        {
            client.BaseAddress = new Uri("https://www.ssnsports.com.tr/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<SsnScraper>());

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

        services.AddScoped<ScrapeIngestionService>();
        services.AddScoped<DealsQueryService>();
        services.AddScoped<PriceHistoryQueryService>();
        services.AddScoped<CouponService>();
        services.AddHostedService<ScrapingBackgroundService>();

        services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/");
        });
        services.AddScoped<SubscriberService>();
        services.AddScoped<DigestService>();
        services.AddScoped<ProductWatchService>();
        services.AddScoped<ProductWatchNotifier>();

        return services;
    }
}

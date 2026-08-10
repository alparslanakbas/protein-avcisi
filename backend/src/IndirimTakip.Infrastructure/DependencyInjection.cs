using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Scraping.Hiq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IndirimTakip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddHttpClient<HiqScraper>(client =>
        {
            client.BaseAddress = new Uri("https://takehiq.com/");
            // Cloudflare, User-Agent'sız istekleri 403 ile engelliyor; tarayıcı gibi görünmemiz gerekiyor.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        });
        services.AddScoped<IBrandScraper>(sp => sp.GetRequiredService<HiqScraper>());
        services.AddScoped<ScrapeIngestionService>();
        services.AddScoped<DealsQueryService>();

        return services;
    }
}

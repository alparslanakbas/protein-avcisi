using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Api.Endpoints;

// Rehber yazıları.
internal static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this WebApplication app, string cachePolicy)
    {
        // "Rehber" bilgi yazıları — SEO/güven amaçlı, kupon deseniyle aynı: elle
        // yazılıp elle eklenen içerik, otomatik üretilmiyor.
        app.MapGet("/api/articles", async (ArticleService articles, CancellationToken ct) =>
        {
            var result = await articles.GetPublishedArticlesAsync(ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);

        app.MapGet("/api/articles/{slug}", async (string slug, ArticleService articles, CancellationToken ct) =>
        {
            var result = await articles.GetBySlugAsync(slug, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).CacheOutput(cachePolicy);
    }
}

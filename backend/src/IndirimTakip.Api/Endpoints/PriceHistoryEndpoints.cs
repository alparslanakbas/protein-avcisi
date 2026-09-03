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

// Fiyat geçmişi grafiği ve kart altındaki mini sparkline'lar.
internal static class PriceHistoryEndpoints
{
    public static void MapPriceHistoryEndpoints(this WebApplication app, string cachePolicy)
    {
        app.MapGet("/api/products/{id:int}/price-history", async (int id, int? days, PriceHistoryQueryService service, CancellationToken ct) =>
        {
            var windowDays = days is null or <= 0 ? 30 : days.Value;
            var result = await service.GetPriceHistoryAsync(id, windowDays, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).CacheOutput(cachePolicy);

        // Ürün kartlarındaki mini sparkline'lar için toplu uç nokta — bir sayfa
        // (24 kart) için tek istek, N+1 yerine. ids sayısı sayfa boyutuyla sınırlı
        // tutulmalı, kötüye kullanıma karşı 100'de sabitliyoruz (NormalizePageSize'daki
        // aynı desen).
        app.MapGet("/api/products/sparklines", async (int[] ids, int? days, PriceHistoryQueryService service, CancellationToken ct) =>
        {
            var windowDays = days is null or <= 0 ? 30 : days.Value;
            var limitedIds = ids.Distinct().Take(100).ToList();
            var result = await service.GetSparklinesAsync(limitedIds, windowDays, ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);
    }
}

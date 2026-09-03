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

// Doğrulanmış kupon/kampanya listesi.
internal static class CouponEndpoints
{
    public static void MapCouponEndpoints(this WebApplication app, string cachePolicy)
    {
        app.MapGet("/api/coupons", async (CouponService coupons, CancellationToken ct) =>
        {
            var result = await coupons.GetActiveCouponsAsync(ct);
            return Results.Ok(result);
        }).CacheOutput(cachePolicy);
    }
}

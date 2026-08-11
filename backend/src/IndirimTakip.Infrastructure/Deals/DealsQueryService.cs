using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

public class DealsQueryService(AppDbContext db)
{
    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(
        int referenceWindowDays = 30,
        string? brandName = null,
        bool onlyDiscounted = true,
        CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);

        var rows = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && (brandName == null || b.Name == brandName)
            select new
            {
                Product = p,
                BrandName = b.Name,
                Latest = p.PriceHistories
                    .OrderByDescending(ph => ph.ScrapedAt)
                    .Select(ph => new { ph.Price, ph.ScrapedAt })
                    .FirstOrDefault(),
                ReferencePrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Max(ph => (decimal?)ph.Price),
            }).ToListAsync(cancellationToken);

        var withPrices = rows.Where(r => r.Latest is not null && r.ReferencePrice is not null);

        if (onlyDiscounted)
            withPrices = withPrices.Where(r => r.Latest!.Price < r.ReferencePrice);

        return withPrices
            .Select(r => new DealDto(
                r.Product.Id,
                r.Product.Name,
                r.Product.Url,
                r.Product.ImageUrl,
                r.Product.Category,
                r.Product.Size,
                r.Product.Flavor,
                r.Product.ServingSizeGrams,
                r.BrandName,
                r.Latest!.Price,
                r.ReferencePrice!.Value,
                Math.Round((r.ReferencePrice.Value - r.Latest.Price) / r.ReferencePrice.Value * 100, 1),
                r.Latest.ScrapedAt))
            .OrderByDescending(d => d.DiscountPercent)
            .ThenBy(d => d.ProductName)
            .ToList();
    }
}

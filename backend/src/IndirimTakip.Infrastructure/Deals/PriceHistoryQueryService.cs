using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

public class PriceHistoryQueryService(AppDbContext db)
{
    public async Task<PriceHistoryDto?> GetPriceHistoryAsync(
        int productId,
        int days,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Include(p => p.Brand)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
            return null;

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var points = await db.PriceHistories
            .Where(ph => ph.ProductId == productId && ph.ScrapedAt >= since)
            .OrderBy(ph => ph.ScrapedAt)
            .Select(ph => new PricePointDto(ph.Price, ph.ScrapedAt))
            .ToListAsync(cancellationToken);

        if (points.Count == 0)
            return new PriceHistoryDto(product.Id, product.Name, product.Brand!.Name, [], 0, 0, 0);

        return new PriceHistoryDto(
            product.Id,
            product.Name,
            product.Brand!.Name,
            points,
            CurrentPrice: points[^1].Price,
            MinPrice: points.Min(p => p.Price),
            MaxPrice: points.Max(p => p.Price));
    }
}

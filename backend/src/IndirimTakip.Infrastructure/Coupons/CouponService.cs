using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Coupons;

public record CreateCouponRequest(string BrandName, string Code, string Description, DateTimeOffset? ValidUntil);

public class CouponService(AppDbContext db)
{
    public async Task<IReadOnlyList<CouponDto>> GetActiveCouponsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Coupons
            .Where(c => c.IsActive && (c.ValidUntil == null || c.ValidUntil >= now))
            .OrderBy(c => c.Brand!.Name)
            .Select(c => new CouponDto(c.Id, c.Brand!.Name, c.Code, c.Description, c.ValidUntil, c.LastVerifiedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CouponDto?> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Name == request.BrandName, cancellationToken);
        if (brand is null)
            return null;

        var coupon = new Coupon
        {
            BrandId = brand.Id,
            Code = request.Code,
            Description = request.Description,
            ValidUntil = request.ValidUntil,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(cancellationToken);

        return new CouponDto(coupon.Id, brand.Name, coupon.Code, coupon.Description, coupon.ValidUntil, coupon.LastVerifiedAt);
    }
}

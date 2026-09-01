using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Coupons;

public record CreateCouponRequest(
    string? BrandName,
    string? Seller,
    // Kodu olmayan kampanyalar için boş bırakılabilir; bkz. Coupon.Code.
    string? Code,
    string Description,
    DateTimeOffset? ValidUntil)
{
    public bool HasExactlyOneTarget =>
        !string.IsNullOrWhiteSpace(BrandName) ^ !string.IsNullOrWhiteSpace(Seller);
}

// IsActive dahil — süresi geçen/yanlış çıkan bir kuponu deaktive etmenin
// API üzerinden hiçbir yolu yoktu, sadece doğrudan DB erişimiyle mümkündü.
public record UpdateCouponRequest(string? Code, string? Description, DateTimeOffset? ValidUntil, bool? IsActive);

public class CouponService(AppDbContext db)
{
    public async Task<IReadOnlyList<CouponDto>> GetActiveCouponsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Coupons
            .Where(c => c.IsActive && (c.ValidUntil == null || c.ValidUntil >= now))
            .OrderBy(c => c.Seller ?? c.Brand!.Name)
            .Select(c => new CouponDto(
                c.Id,
                c.Brand != null ? c.Brand.Name : null,
                c.Seller,
                c.Code,
                c.Description,
                c.ValidUntil,
                c.LastVerifiedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CouponDto?> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.HasExactlyOneTarget)
            throw new ArgumentException("Kupon yalnızca bir markaya veya bir satıcıya bağlanmalıdır.", nameof(request));

        Brand? brand = null;
        if (!string.IsNullOrWhiteSpace(request.BrandName))
        {
            brand = await db.Brands.FirstOrDefaultAsync(b => b.Name == request.BrandName.Trim(), cancellationToken);
            if (brand is null)
                return null;
        }

        var seller = string.IsNullOrWhiteSpace(request.Seller)
            ? null
            : request.Seller.Trim().ToLowerInvariant();

        var coupon = new Coupon
        {
            BrandId = brand?.Id,
            Seller = seller,
            // Boş dize ile NULL aynı şeyi ifade ediyor ("kod yok"); tek bir
            // biçimde saklanıyor ki arayüz iki ayrı boşluk durumu kontrol etmesin.
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Description = request.Description.Trim(),
            ValidUntil = request.ValidUntil,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(coupon, brand?.Name);
    }

    public async Task<CouponDto?> UpdateAsync(int id, UpdateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var coupon = await db.Coupons.Include(c => c.Brand).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (coupon is null)
            return null;

        if (request.Code is not null)
            coupon.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        if (request.Description is not null) coupon.Description = request.Description;
        if (request.ValidUntil is not null) coupon.ValidUntil = request.ValidUntil;
        if (request.IsActive is not null) coupon.IsActive = request.IsActive.Value;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(coupon, coupon.Brand?.Name);
    }

    private static CouponDto ToDto(Coupon coupon, string? brandName) =>
        new(
            coupon.Id,
            brandName,
            coupon.Seller,
            coupon.Code,
            coupon.Description,
            coupon.ValidUntil,
            coupon.LastVerifiedAt);
}

namespace IndirimTakip.Infrastructure.Coupons;

public record CouponDto(
    int Id,
    string BrandName,
    string Code,
    string Description,
    DateTimeOffset? ValidUntil,
    DateTimeOffset LastVerifiedAt);

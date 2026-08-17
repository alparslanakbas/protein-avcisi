namespace IndirimTakip.Infrastructure.Deals;

public record HomepageStatsDto(
    int TotalProducts,
    int DiscountCount,
    int ThirtyDayLowCount,
    DateTimeOffset? LastScanAt);

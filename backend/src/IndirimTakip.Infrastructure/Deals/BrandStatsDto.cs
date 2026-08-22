namespace IndirimTakip.Infrastructure.Deals;

public record BrandStatsDto(
    int TotalProducts,
    int DiscountCount,
    int ThirtyDayLowCount,
    // İndirimdeki ürünlerin ortalama indirim yüzdesi — hiç indirim yoksa null
    // (uydurma bir "%0" değeri yerine, veri yoksa alan boş kalır).
    double? AverageDiscountPercent,
    DateTimeOffset? LastScanAt);

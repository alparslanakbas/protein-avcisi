namespace IndirimTakip.Infrastructure.Deals;

public record CategoryComparisonDto(string Category, decimal? Brand1AvgPrice, int Brand1Count, decimal? Brand2AvgPrice, int Brand2Count);

public record BrandComparisonDto(string Brand1, string Brand2, int Brand1TotalProducts, int Brand2TotalProducts, IReadOnlyList<CategoryComparisonDto> Categories);

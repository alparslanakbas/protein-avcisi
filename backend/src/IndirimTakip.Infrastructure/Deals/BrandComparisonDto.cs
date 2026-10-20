namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// Bir markanın bir kategorideki kilogram başına en uygun ürünü (ValuePickRanker
/// korumalarından geçenler arasından).
/// </summary>
public record ComparisonPickDto(int ProductId, string ProductName, string? Size, decimal Price, decimal PricePerKg);

/// <summary>
/// Kategori satırı. Ortalama fiyat paket boyutuna bakmıyor (300 g'lık ürünleri çok olan
/// marka 5 kg satan rakibinden "ucuz" görünebilir); karşılaştırmanın asıl ölçütü
/// kilogram fiyatı medyanı. Kilogram fiyatı yalnız gramajı bilinen ve korumalardan
/// geçen stoktaki ürünlerden hesaplanıyor; kapsül/tablet kategorilerinde null.
/// </summary>
public record CategoryComparisonDto(
    string Category,
    decimal? Brand1AvgPrice, int Brand1Count,
    decimal? Brand2AvgPrice, int Brand2Count,
    decimal? Brand1MedianPerKg = null, int Brand1KgCount = 0, ComparisonPickDto? Brand1BestPick = null,
    decimal? Brand2MedianPerKg = null, int Brand2KgCount = 0, ComparisonPickDto? Brand2BestPick = null);

/// <summary>Son 30 günde kaydedilen fiyat değişimleri (ardışık taramalar arasında).</summary>
public record PriceActivityDto(int Drops, int Rises, int ChangedProducts);

public record BrandComparisonDto(
    string Brand1, string Brand2,
    int Brand1TotalProducts, int Brand2TotalProducts,
    IReadOnlyList<CategoryComparisonDto> Categories,
    PriceActivityDto? Brand1Activity = null, PriceActivityDto? Brand2Activity = null,
    BrandStatsDto? Brand1Stats = null, BrandStatsDto? Brand2Stats = null);

/// <summary>
/// Markanın bir toz kategorideki kilogram fiyatı konumu: markanın medyanı ile sitedeki bütün
/// markaların medyanı (aynı korumalarla), ve markanın kilogram başına en uygun ürünü.
/// </summary>
public record BrandKgPositionDto(
    string Category,
    decimal? BrandMedianPerKg, int BrandKgCount,
    decimal? MarketMedianPerKg, int MarketKgCount, int MarketBrandCount,
    ComparisonPickDto BestPick);

public record BrandInsightsDto(string Brand, PriceActivityDto Activity, IReadOnlyList<BrandKgPositionDto> Categories);

namespace IndirimTakip.Infrastructure.Deals;

public record DealDto(
    int ProductId,
    string ProductName,
    string ProductUrl,
    string? ImageUrl,
    string? Category,
    string? Size,
    string? Flavor,
    decimal? ServingSizeGrams,
    string BrandName,
    decimal CurrentPrice,
    decimal ReferencePrice,
    decimal DiscountPercent,
    // Markanın kendi beyan ettiği (doğrulanmamış) mağaza indirimi — DiscountPercent'ten
    // (bizim gerçek fiyat geçmişimize dayanan) ayrı, UI'da ayrı etiketlenir.
    decimal? StoreOldPrice,
    decimal? StoreDiscountPercent,
    DateTimeOffset ScrapedAt,
    // Güncel fiyat, aynı 30 günlük referans penceresinin en düşüğüne eşit mi
    // (ReferencePrice'ın Max karşılığı — burada Min).
    bool IsAtThirtyDayLow);

namespace IndirimTakip.Infrastructure.Deals;

public record DealDto(
    int ProductId,
    string ProductName,
    string ProductUrl,
    string? ImageUrl,
    string? Category,
    string? Size,
    string? Flavor,
    string BrandName,
    decimal CurrentPrice,
    decimal ReferencePrice,
    decimal DiscountPercent,
    DateTimeOffset ScrapedAt);

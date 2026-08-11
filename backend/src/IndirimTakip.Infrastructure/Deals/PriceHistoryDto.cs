namespace IndirimTakip.Infrastructure.Deals;

public record PricePointDto(decimal Price, DateTimeOffset ScrapedAt);

public record PriceHistoryDto(
    int ProductId,
    string ProductName,
    string BrandName,
    IReadOnlyList<PricePointDto> Points,
    decimal CurrentPrice,
    decimal MinPrice,
    decimal MaxPrice);

namespace IndirimTakip.Core.Scraping;

public record ScrapedProduct(
    string Name,
    string Url,
    string? ImageUrl,
    string? Category,
    decimal Price);

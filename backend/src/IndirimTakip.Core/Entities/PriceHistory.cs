namespace IndirimTakip.Core.Entities;

public class PriceHistory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Price { get; set; }
    public decimal? StoreOldPrice { get; set; }
    public DateTimeOffset ScrapedAt { get; set; }
}

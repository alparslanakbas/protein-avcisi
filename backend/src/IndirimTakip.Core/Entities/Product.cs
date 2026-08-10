namespace IndirimTakip.Core.Entities;

public class Product
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public Brand? Brand { get; set; }

    public required string Name { get; set; }
    public required string Url { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }

    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}

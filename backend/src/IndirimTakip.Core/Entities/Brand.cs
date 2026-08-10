namespace IndirimTakip.Core.Entities;

public class Brand
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

namespace IndirimTakip.Core.Entities;

// "Rehber" içerikleri (ör. "Kreatin alırken nelere dikkat edilir") — SEO/güven
// amaçlı bilgi yazıları, ürün verisinden bağımsız. Kupon tablosuyla aynı
// desende elle yönetiliyor: otomatik üretilmiyor, admin-key korumalı bir
// endpoint'ten elle ekleniyor.
public class Article
{
    public int Id { get; set; }

    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Summary { get; set; }
    public required string Body { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public bool IsPublished { get; set; } = true;
}

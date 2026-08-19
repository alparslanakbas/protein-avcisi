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
    public string? Size { get; set; }
    public string? Flavor { get; set; }
    public int ClickCount { get; set; }

    // Ürün sayfasındaki "Bu bilgi faydalı mıydı?" oyu — basit bir güven
    // sinyali, auth gerektirmiyor (ClickCount ile aynı desen).
    public int HelpfulYesCount { get; set; }
    public int HelpfulNoCount { get; set; }

    // Gerçek besin değeri tablosundan gelen porsiyon büyüklüğü (gram).
    // Sadece markanın verisi güvenilir şekilde sağladığı ürünlerde dolu.
    public decimal? ServingSizeGrams { get; set; }

    // Markanın kendi sitesinden gelen gerçek ürün açıklaması (düz metin,
    // HTML temizlenmiş) — uydurma değil, sadece marka bunu sağlıyorsa dolu.
    // Bir kez doldurulduktan sonra sonraki taramalarda korunur (bkz.
    // ScrapeIngestionService) — markanın açıklamayı çekmeyen scraper'ları
    // (henüz SSN/Hardline) mevcut değeri sıfırlamaz.
    public string? Description { get; set; }

    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}

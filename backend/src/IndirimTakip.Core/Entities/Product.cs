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

    // Paketten kaç servis çıktığı — markanın DOĞRUDAN beyan ettiği sayı.
    // ProteinOcean bunu variant verisinde ("Servis" attribute'u) veriyor;
    // o markada paket gramajı (Size) hiç gelmediği için servis başı fiyat
    // başka türlü hesaplanamıyordu. Diğer markalarda null — orada hesap
    // Size ÷ ServingSizeGrams üzerinden yapılıyor. İkisi de varsa bu alan
    // önceliklidir (türetilmiş değil, markanın kendi beyanı).
    public int? ServingsPerPackage { get; set; }

    // Markanın kendi sitesinden gelen gerçek ürün açıklaması (düz metin,
    // HTML temizlenmiş) — uydurma değil, sadece marka bunu sağlıyorsa dolu.
    // Bir kez doldurulduktan sonra sonraki taramalarda korunur (bkz.
    // ScrapeIngestionService) — markanın açıklamayı çekmeyen scraper'ları
    // (henüz SSN/Hardline) mevcut değeri sıfırlamaz.
    public string? Description { get; set; }

    // Markanın kendi besin değeri tablosu, normalize edilmiş anahtar/değer
    // JSON'u olarak ("Protein": "24 g" gibi). Marka tablo vermiyorsa null —
    // tahmin üretilmiyor. Karşılaştırma sayfasında "içindekiler" tablosu
    // olarak gösteriliyor.
    public string? NutritionJson { get; set; }

    // Yukarıdaki tablodan ayrıştırılmış porsiyon başı protein (gram).
    // Ayrı bir kolon çünkü "servis başı protein maliyeti" hesabı ve buna
    // göre sıralama/filtreleme JSON içinden yapılamaz. Tabloda protein
    // satırı yoksa null.
    public decimal? ProteinPerServingGrams { get; set; }

    // Besin değeri için ürün sayfasına en son ne zaman BAKILDIĞI — tablo
    // bulunmuş olsun olmasın set ediliyor. Çoğu üründe (aksesuar, bar,
    // atıştırmalık) gerçekten tablo yok; bu alan olmadan backfill her hafta
    // aynı ürünleri sonsuza kadar tekrar denerdi.
    public DateTimeOffset? NutritionCheckedAt { get; set; }

    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}

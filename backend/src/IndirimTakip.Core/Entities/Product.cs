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

    // Son taramada mağazada satın alınabilir miydi?
    //
    // NULL = "bu kaynak stok bilgisi vermiyor" demek, "stokta yok" DEĞİL.
    // Sekiz kaynaktan üçü (HIQ, ProteinOcean, Yeşilmarka) bu bilgiyi
    // veriyor; diğerlerinde alan boş kalır ve arayüzde hiçbir rozet
    // gösterilmez. Üç durumlu olması bilinçli: bilinmeyeni "stokta var"
    // saymak uydurma veri olurdu.
    public bool? InStock { get; set; }

    // Ürünü SATAN mağaza; Brand (üretici) alanından ayrı.
    // NULL = markanın kendi sitesinden alınıyor. Bayi kataloglarında dolu.
    public string? Seller { get; set; }

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

    // Sayfanın İÇERİĞİNİN en son ne zaman gerçekten değiştiği — sitemap'teki
    // <lastmod> bunu kullanıyor.
    //
    // Neden ayrı bir alan: önce son tarama zamanı (PriceHistory.ScrapedAt)
    // kullanılıyordu, ama tarama 6 saatte bir TÜM katalogu ölçtüğü için
    // sitemap'teki 1639 adresin 1591'i aynı damgayı taşıyordu. Google
    // lastmod'u yalnızca tutarlı biçimde doğruysa dikkate alıyor; "1600
    // sayfam aynı anda değişti" diyen bir sitede sinyali tamamen yok
    // sayıyor. Sonuç: hangi sayfanın taranmaya değer olduğuna dair elinde
    // hiçbir ipucu kalmıyordu (bkz. "Keşfedildi - dizine eklenmedi").
    //
    // Burası YALNIZCA gerçek bir değişiklikte güncelleniyor: fiyat gerçekten
    // değiştiyse, ya da isim/kategori/besin değeri/açıklama/puan değiştiyse.
    // Fiyatın aynı değerde yeniden ölçülmesi bir değişiklik DEĞİL.
    public DateTimeOffset? ContentUpdatedAt { get; set; }

    // Markanın KENDİ sitesinde gösterdiği yıldız ortalaması ve kaç kişinin
    // puanladığı. Bizim değerlendirmemiz değil, markanın müşterilerinin —
    // arayüzde de bu şekilde etiketleniyor.
    //
    // Markalar arası kıyaslanabilir DEĞİL: her marka farklı bir yorum
    // sistemi kullanıyor ve hepsinde yorum bırakma koşulu farklı. Bu yüzden
    // sıralamada tek başına puan değil, yorum sayısıyla birlikte kullanılıyor.
    // Yalnızca 4 markada veri var (HIQ, Torq, Yeşilmarka, Hardline);
    // diğerleri yorum toplamıyor, onlarda null kalıyor.
    public decimal? RatingValue { get; set; }
    public int? RatingCount { get; set; }

    // Puan zamanla DEĞİŞİYOR (açıklama ve besin değerinin aksine), bu yüzden
    // "bir kez çekildi, bitti" damgası değil, tazeleme sırası belirleyen bir
    // alan: en eski kontrol edilen ürünler önce yenileniyor.
    public DateTimeOffset? RatingCheckedAt { get; set; }

    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    // ---- Fiyat özeti (önceden hesaplanmış) --------------------------------
    //
    // Bu beş alan PriceHistories'ten TÜRETİLİR, kaynak veri değildir; fiyat
    // geçmişi tek doğru kaynak olmaya devam ediyor. Her taramadan sonra tek
    // bir küme sorgusuyla yeniden hesaplanıyorlar
    // (PriceSummaryRefresher).
    //
    // NEDEN: /api/deals isteğinin %97,7'si PostgreSQL'de geçiyordu, çünkü
    // sorgu 2713 ürünün HER BİRİ için PriceHistories üzerinde 6-8
    // korelasyonlu alt sorgu çalıştırıyordu (son fiyat, 30 günün en yükseği,
    // en düşüğü, indirim yüzdesi hesabında aynı alt sorgular tekrar tekrar).
    // Ölçüm: COUNT 654 ms + veri sorgusu 1.437 ms.
    //
    // PENCERE SABİT 30 GÜN. `days` parametresi 30'dan farklı gelirse sorgu
    // eski canlı hesaba düşüyor — o yol silinmedi, kasıtlı olarak duruyor.

    /// <summary>En son taranan fiyat.</summary>
    public decimal? LatestPrice { get; set; }

    /// <summary>En son taramada mağazanın beyan ettiği eski fiyat.</summary>
    public decimal? LatestStoreOldPrice { get; set; }

    /// <summary>En son fiyat noktasının zamanı. Bayat ürün süzgeci bunu kullanıyor.</summary>
    public DateTimeOffset? LatestScrapedAt { get; set; }

    /// <summary>Son 30 günün EN YÜKSEK fiyatı — "doğrulanmış indirim" bunun üzerinden hesaplanıyor.</summary>
    public decimal? ReferencePrice30 { get; set; }

    /// <summary>Son 30 günün EN DÜŞÜK fiyatı — "30 günün en düşüğü" rozeti.</summary>
    public decimal? LowestPrice30 { get; set; }

    /// <summary>
    /// Özetin en son ne zaman hesaplandığı. Taramadan sonra güncelleniyor;
    /// çok bayatsa sorgu güvenli tarafa geçip canlı hesaba düşebilir.
    /// </summary>
    public DateTimeOffset? PriceSummaryUpdatedAt { get; set; }
}

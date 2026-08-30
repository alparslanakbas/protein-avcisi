namespace IndirimTakip.Core.Scraping;

public record ScrapedProduct(
    string Name,
    string Url,
    string? ImageUrl,
    string? Category,
    decimal Price,
    // Gerçek besin değeri tablosundan gelen porsiyon büyüklüğü (gram). Sadece
    // marka bunu güvenilir şekilde sağlıyorsa doldurulur (şimdilik HIQ) —
    // yoksa uydurmak yerine boş bırakılır.
    decimal? ServingSizeGrams = null,
    // Markanın kendi beyan ettiği eski fiyat (Shopify compare_at_price, OpenCart
    // price-old, vb.) — sadece marka açıkça sunuyorsa doldurulur. Bizim
    // "Doğrulanmış İndirim" hesabımızdan (referans fiyat geçmişi) tamamen ayrı;
    // "Mağaza İndirimi" olarak ayrı ve etiketli gösterilir.
    decimal? StoreOldPrice = null,
    // Markanın kendi sitesinden gelen gerçek ürün açıklaması (düz metin).
    // Sadece marka bunu güvenilir şekilde sağlıyorsa doldurulur (şimdilik HIQ)
    // — yoksa uydurmak yerine null bırakılır.
    string? Description = null,
    // Paketten kaç servis çıktığı — markanın doğrudan beyan ettiği sayı
    // (şimdilik yalnızca ProteinOcean, variant "Servis" attribute'u).
    int? ServingsPerPackage = null,
    // Gerçek besin değeri tablosu, normalize edilmiş JSON (şimdilik yalnızca
    // HIQ — normal taramada, body_html içinde geliyor). Diğer 3 marka için
    // ayrı bir arayüzle (IProductDetailFetcher) haftalık backfill'de doldurulur.
    string? NutritionJson = null,
    // Yukarıdaki tablodan ayrıştırılmış porsiyon başı protein (gram).
    decimal? ProteinPerServingGrams = null,
    // Ürünün GERÇEK üretici markası. Tek markalı scraper'lar bunu hiç
    // doldurmuyor (marka scraper'ın kendisinden geliyor). Çok markalı bir
    // kaynakta (bir bayi kataloğu) her ürün kendi markasını taşıyor: ürün
    // "Supplementler" değil "Optimum Nutrition" markası altında görünmeli,
    // mağaza bağlantısı ise ürünün satıldığı yere gitmeli.
    string? BrandName = null,
    // Ürün şu anda mağazada satın alınabilir mi?
    //
    // NULL = "bilmiyoruz" ve false ile KARIŞTIRILMAMALI. Sekiz kaynaktan
    // yalnızca üçü (HIQ, ProteinOcean, Yeşilmarka) stok bilgisi veriyor;
    // diğerlerinde alan boş bırakılır ve kullanıcıya hiçbir rozet
    // gösterilmez. Bilinmeyeni "stokta var" saymak uydurma veriyle aynı
    // kapıya çıkardı.
    //
    // Stokta olmayan ürün artık taramadan DÜŞMÜYOR. HIQ'da düşüyordu ve
    // ürünün fiyat geçmişinde günlerce boşluk oluşuyordu; stok geri
    // geldiğinde seri kopuk kalıyordu. Sitenin iddiası kesintisiz gerçek
    // fiyat geçmişi olduğu için bu boşluk doğrudan o iddiayı zayıflatıyordu.
    bool? InStock = null);

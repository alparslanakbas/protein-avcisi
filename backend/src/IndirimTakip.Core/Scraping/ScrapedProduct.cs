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
    decimal? ProteinPerServingGrams = null);

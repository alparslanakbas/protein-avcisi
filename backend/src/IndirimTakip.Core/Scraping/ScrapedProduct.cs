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
    decimal? StoreOldPrice = null);

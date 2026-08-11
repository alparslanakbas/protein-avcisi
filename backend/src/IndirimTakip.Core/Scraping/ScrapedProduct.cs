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
    decimal? ServingSizeGrams = null);

namespace IndirimTakip.Infrastructure.Deals;

public record DealDto(
    int ProductId,
    string ProductName,
    string ProductUrl,
    string? ImageUrl,
    string? Category,
    string? Size,
    string? Flavor,
    decimal? ServingSizeGrams,
    // Paketten kaç servis çıktığı — markanın doğrudan beyanı (şimdilik
    // yalnızca ProteinOcean; o markada paket gramajı hiç gelmediği için
    // servis başı fiyat ancak buradan hesaplanabiliyor).
    int? ServingsPerPackage,
    // Markanın kendi sitesinden gelen gerçek ürün açıklaması — sadece marka
    // bunu sağlıyorsa dolu (şimdilik HIQ), yoksa null (uydurma yok).
    string? Description,
    // Gerçek besin değeri tablosu, normalize edilmiş JSON — sadece marka bunu
    // güvenilir şekilde sağlıyorsa dolu (HIQ + haftalık backfill ile SSN/
    // Hardline; ProteinOcean bilinçli olarak dışarıda, veri yapısı güvenilir
    // ayrıştırılamıyor). Karşılaştırma sayfasında "İçindekiler" tablosu için.
    string? NutritionJson,
    decimal? ProteinPerServingGrams,
    string BrandName,
    decimal CurrentPrice,
    decimal ReferencePrice,
    decimal DiscountPercent,
    // Markanın kendi beyan ettiği (doğrulanmamış) mağaza indirimi — DiscountPercent'ten
    // (bizim gerçek fiyat geçmişimize dayanan) ayrı, UI'da ayrı etiketlenir.
    decimal? StoreOldPrice,
    decimal? StoreDiscountPercent,
    DateTimeOffset ScrapedAt,
    // Güncel fiyat, aynı 30 günlük referans penceresinin en düşüğüne eşit mi
    // (ReferencePrice'ın Max karşılığı — burada Min) VE pencerede gerçekten
    // bir fiyat farkı var mı (ThirtyDayLowPrice < ReferencePrice). İkinci şart
    // olmadan, hiç fiyatı değişmemiş bir ürün (Min=Max=Latest) trivially
    // "30 günün dibi" sayılırdı — bkz. DealsQueryService.MapToDealDto.
    bool IsAtThirtyDayLow,
    // Aşağıdaki iki alanı YALNIZCA GetProductByIdAsync dolduruyor (tekil ürün
    // sayfası); listelerde donmuş kayıtlar zaten gizlendiği için orada anlamı
    // yok ve varsayılan değerlerinde kalıyorlar.
    //
    // Kayıt, markanın taramasında artık dönmüyor (bkz. StaleThreshold). Sayfa
    // çalışmaya devam ediyor — fiyat geçmişi hâlâ değerli — ama dizine
    // eklenmemesi gerekiyor.
    bool IsStale = false,
    // Aynı marka + aynı isimli güncel kayıt. Marka çoğu zaman ürünü silmiyor,
    // yalnızca adresini değiştiriyor; bu durumda eski adres yeni kayda
    // yönlendirilmeli ki iki sayfa birbiriyle çakışmasın.
    int? ReplacementProductId = null);

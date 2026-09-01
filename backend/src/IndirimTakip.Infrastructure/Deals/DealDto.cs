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
    int? ReplacementProductId = null,
    // Markanın KENDİ sitesindeki yıldız ortalaması ve puanlayan sayısı —
    // bizim değerlendirmemiz değil. Arayüzde de bu şekilde etiketleniyor.
    // Yalnızca yorum toplayan markalarda dolu; markalar arası kıyaslanabilir
    // değil (her biri farklı bir yorum sistemi kullanıyor).
    decimal? RatingValue = null,
    int? RatingCount = null,
    // Son taramada mağazada satın alınabilir miydi?
    //
    // NULL = "bu kaynak stok bilgisi vermiyor", false ile karıştırılmamalı.
    // Sekiz kaynaktan üçü bu bilgiyi veriyor; diğerlerinde arayüz hiçbir
    // rozet göstermiyor. Stokta olmayan ürün listelerden ÇIKARILMIYOR:
    // fiyat geçmişi kesintisiz kalsın diye taranmaya devam ediyor ve
    // "Tükendi" rozetiyle gösteriliyor.
    bool? InStock = null,
    // Ürünü satan mağaza; NULL ise markanın kendi sitesi.
    string? Seller = null,
    // ORTAKLIK KODU EKLENMİŞ mağaza adresi — "Mağazaya git" bağlantısının
    // gideceği yer.
    //
    // Neden DTO'da: bağlantı eskiden kendi sitemizdeki /go/{id} ucuna
    // gidiyordu, o da 302 ile mağazaya atıyordu. Kurulu PWA'da bu araya
    // giren yönlendirme geri tuşunu ÖLDÜRÜYORDU: yeni tarama bağlamının
    // geçmişinde yalnızca yönlendirme zinciri kalıyor, geri basınca bağlam
    // kapanıyor ve kullanıcı uygulamadan çıkmış oluyordu (kullanıcı bildirdi,
    // ölçümle doğrulandı). Doğrudan dış adrese giden bağlantıda sorun yok —
    // aynı PWA'da yönlendirmesiz bir dış bağlantı test edildi, geri çalıştı.
    //
    // /go/{id} KALDIRILMADI: dizine girmiş adresler, e-postalar ve eski
    // istemciler için çalışmaya devam ediyor.
    string? StoreUrl = null,
    // Bu sayfa BAŞKA bir ürün sayfasının kopyasıysa, asıl sayfanın ürün Id'si.
    //
    // Markaların kendi siteleri aynı ürünü birden çok adreste yayınlıyor
    // (eski adres, "copy-of-..." taslağı, sonuna "-1" eklenmiş tekrar) ve
    // her adres bizde ayrı bir ürün satırı oluyor. Sayfalar birbirinin aynısı
    // olduğu için Google bunları kopya sayıp kendi standart sayfasını
    // seçiyordu (GSC, 1 Eylül: 21 sayfada doğrulama başarısız).
    //
    // Dolu olduğunda ürün sayfası canonical'ı asıl sayfayı gösteriyor.
    // NULL = bu sayfa zaten asıl sayfa (ya da hiç kopyası yok).
    int? CanonicalProductId = null);

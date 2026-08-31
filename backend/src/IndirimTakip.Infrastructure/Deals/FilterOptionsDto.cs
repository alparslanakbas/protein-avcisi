namespace IndirimTakip.Infrastructure.Deals;

// Sellers: ürünün satın alındığı yer. Marka (üretici) ile aynı şey değil —
// bir bayi kataloğunda BigJoy ürünü protein7.com'dan satılıyor olabilir.
// Listenin ilk elemanı, markanın kendi sitesinden satılan ürünleri seçmek
// için kullanılan etiket (DealsQueryService.BrandDirectSellerLabel).
public record FilterOptionsDto(
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Sellers);

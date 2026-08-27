namespace IndirimTakip.Infrastructure.Deals;

public record BrandStatsDto(
    int TotalProducts,
    int DiscountCount,
    int ThirtyDayLowCount,
    // İndirimdeki ürünlerin ortalama indirim yüzdesi — hiç indirim yoksa null
    // (uydurma bir "%0" değeri yerine, veri yoksa alan boş kalır).
    double? AverageDiscountPercent,
    DateTimeOffset? LastScanAt,
    // Kapsamdaki ürünlerin ortalama güncel fiyatı. Marka × kategori
    // sayfalarında, aynı kategorinin geneliyle karşılaştırıp markanın o
    // kategoride nerede durduğunu söylemek için — o sayfaların tek özgün
    // içeriği bu. Ürün yoksa null.
    decimal? AveragePrice = null);

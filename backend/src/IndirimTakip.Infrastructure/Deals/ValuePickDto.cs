namespace IndirimTakip.Infrastructure.Deals;

public sealed record ValuePickDto(
    int ProductId,
    string ProductName,
    string BrandName,
    string? ImageUrl,
    string? Size,
    // Ürünü satan mağaza; NULL ise markanın kendi sitesi.
    string? Seller,
    decimal CurrentPrice,
    decimal PricePerKg,
    // Son 30 günün en yüksek fiyatı; indirim bunun üzerinden hesaplanıyor.
    decimal ReferencePrice,
    // Bizim fiyat geçmişimize dayanan indirim (DealDto ile aynı hesap); yoksa 0.
    decimal DiscountPercent,
    bool IsAtThirtyDayLow,
    // Stokta olmayanlar listeye hiç girmiyor; null = kaynak stok bilgisi vermiyor.
    bool? InStock,
    // Ortaklık kodu eklenmiş mağaza adresi (bkz. DealDto.StoreUrl).
    string StoreUrl,
    // Takip başladığından beri en düşük fiyattaysa takibin başladığı an (bkz. DealDto.LowestSince, TakipDibi).
    DateTimeOffset? LowestSince = null);

/// <param name="EligibleCount">Kilogram fiyatı hesaplanabilen, korumalardan geçen ürün sayısı (her markadan bir ürün seçilmeden önce).</param>
public sealed record ValuePicksDto(IReadOnlyList<ValuePickDto> Items, int EligibleCount);

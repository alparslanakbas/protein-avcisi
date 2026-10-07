using IndirimTakip.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// "Hangi takviye?" sayfalarının ürün listesi: bir kategoride kilogram
/// fiyatına göre en uygun ürünler (bkz. ValuePickRanker).
///
/// DealsQueryService'ten BİLEREK AYRI: o dosya bu projede üretimi iki kez
/// düşürdü ve sayfalama/süzgeç/arama mantığı taşıyor. Buradaki iş salt okunur
/// tek bir sorgu; sıralama ağırlık metnini ayrıştırmayı gerektirdiği için
/// SQL'e çevrilemiyor, bellekte yapılıyor (kategori başına en çok ~1.100 satır).
/// </summary>
public sealed class ValuePicksQueryService(
    AppDbContext db,
    IOptions<AffiliateOptions> affiliateOptions,
    ProductImageOptions gorselAyarlari)
{
    public async Task<ValuePicksDto> GetAsync(
        string category, string? type, int count, CancellationToken cancellationToken = default)
    {
        var bayatlikSiniri = DateTimeOffset.UtcNow.Subtract(DealsQueryService.StaleThreshold);

        // Liste sorgularıyla aynı taban: aktif marka, gizlenmemiş ürün (global
        // sorgu filtresi), 48 saat içinde taranmış, fiyat özeti dolu.
        var satirlar = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
                  && p.Category == category
                  && p.LatestPrice != null && p.LatestPrice > 0
                  && p.ReferencePrice30 != null && p.LowestPrice30 != null
                  && p.LatestScrapedAt != null && p.LatestScrapedAt >= bayatlikSiniri
                  && p.InStock != false
                  && p.Size != null
            select new
            {
                p.Id,
                p.BrandId,
                BrandName = b.Name,
                p.Name,
                p.Size,
                p.Url,
                p.ImageUrl,
                p.LocalImagePath,
                p.Seller,
                p.InStock,
                Price = p.LatestPrice!.Value,
                ReferencePrice = p.ReferencePrice30!.Value,
                LowestPrice = p.LowestPrice30!.Value,
                p.TrackedSince,
                p.LowestTrackedPrice,
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var siralama = ValuePickRanker.Rank(
            satirlar.Select(s => new ValuePickCandidate(s.Id, s.BrandId, s.BrandName, s.Name, s.Size, s.Price, s.InStock)),
            category, type, count);

        var satirHaritasi = satirlar.ToDictionary(s => s.Id);
        var simdi = DateTimeOffset.UtcNow;
        var ogeler = siralama.Picks
            .Select(secim =>
            {
                var s = satirHaritasi[secim.Candidate.ProductId];
                var otuzGununEnDusugu = s.Price <= s.LowestPrice && s.LowestPrice < s.ReferencePrice;
                return new ValuePickDto(
                    s.Id,
                    s.Name,
                    s.BrandName,
                    ProductImageStore.GenelAdres(s.LocalImagePath, gorselAyarlari.TabanAdres) ?? s.ImageUrl,
                    s.Size,
                    s.Seller,
                    s.Price,
                    secim.PricePerKg,
                    s.ReferencePrice,
                    // DealsQueryService.MapToDealDto ile aynı hesap: sitenin
                    // başka yerinde "%12 indirim" diyen ürün burada farklı
                    // bir sayı göstermemeli.
                    s.ReferencePrice > 0 ? Math.Round((s.ReferencePrice - s.Price) / s.ReferencePrice * 100, 1) : 0m,
                    otuzGununEnDusugu,
                    s.InStock,
                    AffiliateLinkBuilder.Apply(s.Url, s.BrandName, affiliateOptions.Value),
                    TakipDibi.Baslangic(otuzGununEnDusugu, s.Price, s.TrackedSince, s.LowestTrackedPrice, simdi));
            })
            .ToList();

        return new ValuePicksDto(ogeler, siralama.EligibleCount);
    }
}

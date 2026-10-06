using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// Verilen ürünlerin en güncel fiyat noktası, ürün başına bir tane.
/// </summary>
/// <remarks>
/// Projeksiyon içinde <c>p.PriceHistories.OrderByDescending(...).FirstOrDefault()</c> yazmak EF'te BÜTÜN
/// <c>PriceHistories</c> üzerinde <c>ROW_NUMBER() OVER (PARTITION BY "ProductId" ...)</c> penceresine dönüşüyor,
/// istenen ürünlere sonra bağlanıyor (6 Ekim ölçümü: tek ürün 0,4-0,9 sn, iki ürünlük favori listesi 0,75 sn).
/// Burada süzgeç pencereden önce: yalnız istenen ürünlerin satırları okunuyor.
/// </remarks>
internal static class SonFiyatSorgusu
{
    public static Task<Dictionary<int, PriceHistory>> UrunlerIcinAsync(
        AppDbContext db, IReadOnlyCollection<int> urunIdleri, CancellationToken cancellationToken) =>
        db.PriceHistories
            .AsNoTracking()
            .Where(ph => urunIdleri.Contains(ph.ProductId))
            .GroupBy(ph => ph.ProductId)
            .Select(g => g.OrderByDescending(ph => ph.ScrapedAt).First())
            .ToDictionaryAsync(ph => ph.ProductId, cancellationToken);
}

using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

public class PriceHistoryQueryService(AppDbContext db)
{
    public async Task<PriceHistoryDto?> GetPriceHistoryAsync(
        int productId,
        int days,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Include(p => p.Brand)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
            return null;

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var points = await db.PriceHistories
            .Where(ph => ph.ProductId == productId && ph.ScrapedAt >= since)
            .OrderBy(ph => ph.ScrapedAt)
            .Select(ph => new PricePointDto(ph.Price, ph.ScrapedAt))
            .ToListAsync(cancellationToken);

        if (points.Count == 0)
            return new PriceHistoryDto(product.Id, product.Name, product.Brand!.Name, [], 0, 0, 0);

        return new PriceHistoryDto(
            product.Id,
            product.Name,
            product.Brand!.Name,
            points,
            CurrentPrice: points[^1].Price,
            MinPrice: points.Min(p => p.Price),
            MaxPrice: points.Max(p => p.Price));
    }

    // Ürün kartlarındaki mini sparkline'lar için — Faz 1'de N+1 istek riski
    // yüzünden bilinçli olarak ertelenmişti (bkz. CLAUDE.md). Bir sayfa
    // (24 kart) için tek istekte tüm fiyat noktalarını dönüyor. Anonim tip +
    // bellek içinde gruplama kullanıyor (DealsQueryService'te daha önce
    // yaşanan "adlandırılmış record EF Core'a çevrilemedi" bug'ıyla aynı
    // hatayı tekrarlamamak için, bkz. CLAUDE.md).
    public async Task<IReadOnlyList<ProductSparklineDto>> GetSparklinesAsync(
        IReadOnlyList<int> productIds,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return [];

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var rows = await db.PriceHistories
            .Where(ph => productIds.Contains(ph.ProductId) && ph.ScrapedAt >= since)
            .OrderBy(ph => ph.ScrapedAt)
            .Select(ph => new { ph.ProductId, ph.Price, ph.ScrapedAt })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ProductId)
            .Select(g => new ProductSparklineDto(
                g.Key,
                KosuSinirlari(g.Select(r => new PricePointDto(r.Price, r.ScrapedAt)).ToList())))
            .ToList();
    }

    /// <summary>
    /// Aynı fiyatın art arda geldiği noktalardan yalnızca koşunun ilk ve son
    /// noktasını bırakır; çizim değişmiyor.
    /// </summary>
    /// <remarks>
    /// Kart grafiği noktaları zamana göre yerleştirip düz çizgiyle birleştiriyor
    /// (frontend <c>spark-chart.ts</c>). Aynı fiyatlı bir koşunun ara noktaları,
    /// koşunun ilk ve son noktasını birleştiren yatay çizginin üstünde duruyor;
    /// atılınca çizgi de altındaki alan da birebir aynı kalıyor. Fiyat günde
    /// birkaç kez taranıp seyrek değiştiği için 30 günlük ~139 nokta çoğu üründe
    /// birkaç noktaya iniyor.
    ///
    /// Neden (6 Ekim): ürün sayfası ana listeyi de render ettiği için 24 kartın
    /// sparkline'ı sayfaya gömülen aktarım verisine biniyordu (219 KB; ölçülen
    /// ürün sayfasında HTML'in %86'sı aktarım verisiydi). "Günde bir nokta" seyreltmesi
    /// seçilmedi: gün içindeki bir düşüşü siliyor ve çizimi değiştiriyordu.
    /// </remarks>
    internal static List<PricePointDto> KosuSinirlari(List<PricePointDto> noktalar)
    {
        var sonuc = new List<PricePointDto>(noktalar.Count);
        for (var i = 0; i < noktalar.Count; i++)
        {
            var oncekiAyni = i > 0 && noktalar[i - 1].Price == noktalar[i].Price;
            var sonrakiAyni = i < noktalar.Count - 1 && noktalar[i + 1].Price == noktalar[i].Price;
            if (!(oncekiAyni && sonrakiAyni))
                sonuc.Add(noktalar[i]);
        }
        return sonuc;
    }
}

using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

public class DealsQueryService(AppDbContext db)
{
    public async Task<PagedResult<DealDto>> GetDealsAsync(
        int referenceWindowDays,
        string[]? brands,
        string[]? categories,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool onlyDiscounted,
        bool onlyStoreDiscounted,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);

        var query =
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
            select new
            {
                Product = p,
                BrandName = b.Name,
                Latest = p.PriceHistories
                    .OrderByDescending(ph => ph.ScrapedAt)
                    .Select(ph => new { ph.Price, ph.ScrapedAt, ph.StoreOldPrice })
                    .FirstOrDefault(),
                ReferencePrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Max(ph => (decimal?)ph.Price),
            };

        if (brands is { Length: > 0 })
            query = query.Where(r => brands.Contains(r.BrandName));

        if (categories is { Length: > 0 })
            query = query.Where(r => r.Product.Category != null && categories.Contains(r.Product.Category));

        var searchTerm = search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            // Kategori "protein-tozu" gibi tire'li saklanıyor; "protein tozu" araması
            // da eşleşsin diye tire'leri boşluğa çevirip karşılaştırıyoruz.
            query = query.Where(r =>
                r.Product.Name.ToLower().Contains(searchTerm) ||
                r.BrandName.ToLower().Contains(searchTerm) ||
                (r.Product.Category != null && r.Product.Category.Replace("-", " ").ToLower().Contains(searchTerm)) ||
                (r.Product.Size != null && r.Product.Size.ToLower().Contains(searchTerm)) ||
                (r.Product.Flavor != null && r.Product.Flavor.ToLower().Contains(searchTerm)));
        }

        query = query.Where(r => r.Latest != null && r.ReferencePrice != null);

        if (onlyDiscounted)
            query = query.Where(r => r.Latest!.Price < r.ReferencePrice);

        if (onlyStoreDiscounted)
            query = query.Where(r => r.Latest!.StoreOldPrice != null && r.Latest.StoreOldPrice > r.Latest.Price);

        if (minPrice is not null)
            query = query.Where(r => r.Latest!.Price >= minPrice);

        if (maxPrice is not null)
            query = query.Where(r => r.Latest!.Price <= maxPrice);

        var totalCount = await query.CountAsync(cancellationToken);

        // Mağaza kampanyaları görünümünde mağazanın beyan ettiği indirim oranına göre,
        // diğer görünümlerde bizim doğrulanmış indirim oranımıza göre sırala.
        var orderedQuery = onlyStoreDiscounted
            ? query.OrderByDescending(r => (r.Latest!.StoreOldPrice!.Value - r.Latest.Price) / r.Latest.StoreOldPrice.Value)
            : query.OrderByDescending(r => (r.ReferencePrice!.Value - r.Latest!.Price) / r.ReferencePrice.Value);

        var pageRows = await orderedQuery
            .ThenBy(r => r.Product.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(r => new DealDto(
                r.Product.Id,
                r.Product.Name,
                r.Product.Url,
                r.Product.ImageUrl,
                r.Product.Category,
                r.Product.Size,
                r.Product.Flavor,
                r.Product.ServingSizeGrams,
                r.BrandName,
                r.Latest!.Price,
                r.ReferencePrice!.Value,
                Math.Round((r.ReferencePrice.Value - r.Latest.Price) / r.ReferencePrice.Value * 100, 1),
                r.Latest.StoreOldPrice,
                r.Latest.StoreOldPrice is decimal storeOld && storeOld > 0
                    ? Math.Round((storeOld - r.Latest.Price) / storeOld * 100, 1)
                    : null,
                r.Latest.ScrapedAt))
            .ToList();

        return new PagedResult<DealDto>(items, totalCount, page, pageSize);
    }

    // Ürün detay sayfası (/urun/:id) için tekil ürün sorgusu — hem paylaşılan
    // bir linkle direkt gelen ziyaretçide hem SSR'da (liste henüz yüklenmemiş
    // olabilir) ürünü listeden bağımsız çekebilmek için gerekli.
    public async Task<DealDto?> GetProductByIdAsync(
        int productId, int referenceWindowDays = 30, CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);

        var row = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && p.Id == productId
            select new
            {
                Product = p,
                BrandName = b.Name,
                Latest = p.PriceHistories
                    .OrderByDescending(ph => ph.ScrapedAt)
                    .Select(ph => new { ph.Price, ph.ScrapedAt, ph.StoreOldPrice })
                    .FirstOrDefault(),
                ReferencePrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Max(ph => (decimal?)ph.Price),
            }).FirstOrDefaultAsync(cancellationToken);

        if (row?.Latest is null || row.ReferencePrice is null)
            return null;

        return new DealDto(
            row.Product.Id, row.Product.Name, row.Product.Url, row.Product.ImageUrl,
            row.Product.Category, row.Product.Size, row.Product.Flavor, row.Product.ServingSizeGrams,
            row.BrandName, row.Latest.Price, row.ReferencePrice.Value,
            Math.Round((row.ReferencePrice.Value - row.Latest.Price) / row.ReferencePrice.Value * 100, 1),
            row.Latest.StoreOldPrice,
            row.Latest.StoreOldPrice is decimal storeOld && storeOld > 0
                ? Math.Round((storeOld - row.Latest.Price) / storeOld * 100, 1)
                : null,
            row.Latest.ScrapedAt);
    }

    public async Task<FilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var brands = await db.Brands
            .Where(b => b.IsActive)
            .Select(b => b.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var categories = await db.Products
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return new FilterOptionsDto(brands, categories);
    }
}

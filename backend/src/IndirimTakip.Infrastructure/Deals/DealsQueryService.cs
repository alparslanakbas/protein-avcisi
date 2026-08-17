using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

// GetDealsAsync/GetProductByIdAsync/GetDealsByIdsAsync'in ortak DealDto'ya
// çevirme mantığı (indirim yüzdesi vb.) burada tek yerde toplanıyor. ÖNEMLİ:
// bu record sadece materialize edildikten (ToListAsync/FirstOrDefaultAsync)
// SONRA, bellek içinde kuruluyor — EF Core'un SQL'e çevirmesi gereken bir
// projeksiyonun parçası DEĞİL. İlk halinde "Latest" iç içe ayrı bir record
// (PricePointRow) olarak doğrudan sorgu projeksiyonunda kuruluyordu; sonraki
// .Where() filtreleri o iç içe record'un alanlarına (r.Latest!.Price vb.)
// eriştiğinde EF Core "could not be translated" hatasıyla 500 dönüyordu
// (canlıda yakalandı). Çözüm: sorgu tarafında filtreleme/sıralama düz bir
// anonim tip + PriceHistory ENTITY'si (Latest) üzerinden yapılıyor — EF
// Core'un native desteklediği bir kalıp — DealRow'a çevirme işi listeye
// dönüştükten sonra yapılıyor.
internal sealed record DealRow(Product Product, string BrandName, PriceHistory Latest, decimal ReferencePrice, decimal ThirtyDayLowPrice);

public class DealsQueryService(AppDbContext db)
{
    private static DealDto MapToDealDto(DealRow row)
    {
        var latest = row.Latest;
        var referencePrice = row.ReferencePrice;
        return new DealDto(
            row.Product.Id, row.Product.Name, row.Product.Url, row.Product.ImageUrl,
            row.Product.Category, row.Product.Size, row.Product.Flavor, row.Product.ServingSizeGrams,
            row.BrandName, latest.Price, referencePrice,
            Math.Round((referencePrice - latest.Price) / referencePrice * 100, 1),
            latest.StoreOldPrice,
            latest.StoreOldPrice is decimal storeOld && storeOld > 0
                ? Math.Round((storeOld - latest.Price) / storeOld * 100, 1)
                : null,
            latest.ScrapedAt,
            latest.Price <= row.ThirtyDayLowPrice);
    }

    public async Task<PagedResult<DealDto>> GetDealsAsync(
        int referenceWindowDays,
        string[]? brands,
        string[]? categories,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool onlyDiscounted,
        bool onlyStoreDiscounted,
        string? sortBy,
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
                Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).FirstOrDefault(),
                ReferencePrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Max(ph => (decimal?)ph.Price),
                ThirtyDayLowPrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Min(ph => (decimal?)ph.Price),
            };

        if (brands is { Length: > 0 })
            query = query.Where(r => brands.Contains(r.BrandName));

        if (categories is { Length: > 0 })
            query = query.Where(r => r.Product.Category != null && categories.Contains(r.Product.Category));

        var searchTerm = search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            // "kreatin" yazınca "creatine" geçen ürünleri de bulsun diye
            // (ve tersi) eşanlamlı terimlerle arama terimini genişletiyoruz.
            var synonyms = ProductAttributeParser.GetSearchSynonyms(searchTerm);
            var searchTerms = synonyms.Count > 0
                ? synonyms.Append(searchTerm).Distinct().ToArray()
                : [searchTerm];

            // Kategori "protein-tozu" gibi tire'li saklanıyor; "protein tozu" araması
            // da eşleşsin diye tire'leri boşluğa çevirip karşılaştırıyoruz.
            query = query.Where(r =>
                searchTerms.Any(t => r.Product.Name.ToLower().Contains(t)) ||
                searchTerms.Any(t => r.BrandName.ToLower().Contains(t)) ||
                (r.Product.Category != null && searchTerms.Any(t => r.Product.Category.Replace("-", " ").ToLower().Contains(t))) ||
                (r.Product.Size != null && searchTerms.Any(t => r.Product.Size.ToLower().Contains(t))) ||
                (r.Product.Flavor != null && searchTerms.Any(t => r.Product.Flavor.ToLower().Contains(t))));
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

        // Kullanıcı bir sıralama seçtiyse onu uygula; seçmediyse mağaza
        // kampanyaları görünümünde mağazanın beyan ettiği indirim oranına,
        // diğer görünümlerde bizim doğruladığımız indirim oranına göre sırala.
        var orderedQuery = sortBy switch
        {
            "name_asc" => query.OrderBy(r => r.Product.Name),
            "name_desc" => query.OrderByDescending(r => r.Product.Name),
            "price_asc" => query.OrderBy(r => r.Latest!.Price).ThenBy(r => r.Product.Name),
            "price_desc" => query.OrderByDescending(r => r.Latest!.Price).ThenBy(r => r.Product.Name),
            _ => onlyStoreDiscounted
                ? query.OrderByDescending(r => (r.Latest!.StoreOldPrice!.Value - r.Latest.Price) / r.Latest.StoreOldPrice.Value).ThenBy(r => r.Product.Name)
                : query.OrderByDescending(r => (r.ReferencePrice!.Value - r.Latest!.Price) / r.ReferencePrice.Value).ThenBy(r => r.Product.Name),
        };

        var pageRows = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(r => new DealRow(r.Product, r.BrandName, r.Latest!, r.ReferencePrice!.Value, r.ThirtyDayLowPrice!.Value))
            .Select(MapToDealDto)
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
                Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).FirstOrDefault(),
                ReferencePrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Max(ph => (decimal?)ph.Price),
                ThirtyDayLowPrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Min(ph => (decimal?)ph.Price),
            }).FirstOrDefaultAsync(cancellationToken);

        if (row?.Latest is null || row.ReferencePrice is null || row.ThirtyDayLowPrice is null)
            return null;

        return MapToDealDto(new DealRow(row.Product, row.BrandName, row.Latest, row.ReferencePrice.Value, row.ThirtyDayLowPrice.Value));
    }

    // Favoriler listesi (/favorilerim) için — belirli bir ürün ID kümesini,
    // sayfalama/sıralama olmadan toplu çekiyor. Ölçek küçük (bir kişinin
    // favori listesi onlarca ürünü geçmez) bu yüzden basit tutuldu.
    public async Task<IReadOnlyList<DealDto>> GetDealsByIdsAsync(
        IReadOnlyCollection<int> productIds, int referenceWindowDays = 30, CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);

        var rows = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && productIds.Contains(p.Id)
            select new
            {
                Product = p,
                BrandName = b.Name,
                Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).FirstOrDefault(),
                ReferencePrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Max(ph => (decimal?)ph.Price),
                ThirtyDayLowPrice = p.PriceHistories
                    .Where(ph => ph.ScrapedAt >= referenceSince)
                    .Min(ph => (decimal?)ph.Price),
            }).ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Latest != null && r.ReferencePrice != null && r.ThirtyDayLowPrice != null)
            .Select(r => new DealRow(r.Product, r.BrandName, r.Latest!, r.ReferencePrice!.Value, r.ThirtyDayLowPrice!.Value))
            .Select(MapToDealDto)
            .ToList();
    }

    // Marka karşılaştırma sayfaları (/karsilastir/x-vs-y) için — kategori
    // bazında ortalama güncel fiyat karşılaştırması. Statik/elle yazılan
    // bir içerik değil, her istekte canlı veriden hesaplanıyor — yeni bir
    // marka/ürün eklendikçe otomatik güncel kalır.
    public async Task<BrandComparisonDto?> GetBrandComparisonAsync(string brand1, string brand2, CancellationToken cancellationToken = default)
    {
        var b1 = await db.Brands.FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand1.ToLower(), cancellationToken);
        var b2 = await db.Brands.FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand2.ToLower(), cancellationToken);
        if (b1 is null || b2 is null || b1.Id == b2.Id)
            return null;

        var avg1 = await GetCategoryAveragesAsync(b1.Id, cancellationToken);
        var avg2 = await GetCategoryAveragesAsync(b2.Id, cancellationToken);

        var categories = avg1.Keys.Union(avg2.Keys)
            .OrderBy(c => c)
            .Select(c =>
            {
                avg1.TryGetValue(c, out var v1);
                avg2.TryGetValue(c, out var v2);
                return new CategoryComparisonDto(
                    c,
                    v1.count > 0 ? Math.Round(v1.avg, 2) : null, v1.count,
                    v2.count > 0 ? Math.Round(v2.avg, 2) : null, v2.count);
            })
            .ToList();

        var total1 = await db.Products.CountAsync(p => p.BrandId == b1.Id, cancellationToken);
        var total2 = await db.Products.CountAsync(p => p.BrandId == b2.Id, cancellationToken);

        return new BrandComparisonDto(b1.Name, b2.Name, total1, total2, categories);
    }

    private async Task<Dictionary<string, (decimal avg, int count)>> GetCategoryAveragesAsync(int brandId, CancellationToken cancellationToken)
    {
        var rows = await (
            from p in db.Products
            where p.BrandId == brandId && p.Category != null
            select new
            {
                Category = p.Category!,
                Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => (decimal?)ph.Price).FirstOrDefault(),
            }).ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Latest is not null)
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => (g.Average(r => r.Latest!.Value), g.Count()));
    }

    // sitemap.xml üretimi için hafif bir liste — DealDto'daki fiyat
    // hesaplarına gerek yok, sadece URL kurmak için Id ve son tarama
    // zamanı (lastmod) yeterli.
    public async Task<IReadOnlyList<SitemapEntryDto>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        return await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
            select new SitemapEntryDto(
                p.Id,
                p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    // Ana sayfadaki "canlı tarama şeridi" için — her istekte canlı hesaplanan
    // özet sayılar (GetBrandComparisonAsync'teki aynı "sabit içerik değil,
    // DB'den canlı hesapla" desende). DiscountCount/ThirtyDayLowCount, GetDealsAsync'in
    // onlyDiscounted / IsAtThirtyDayLow ile AYNI referans pencere mantığını kullanır,
    // sadece burada tek bir toplu geçişte sayılıyor.
    public async Task<HomepageStatsDto> GetHomepageStatsAsync(int referenceWindowDays = 30, CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);

        var totalProducts = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
            select p.Id).CountAsync(cancellationToken);

        var rows = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
            select new
            {
                Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => (decimal?)ph.Price).FirstOrDefault(),
                ReferencePrice = p.PriceHistories.Where(ph => ph.ScrapedAt >= referenceSince).Max(ph => (decimal?)ph.Price),
                ThirtyDayLowPrice = p.PriceHistories.Where(ph => ph.ScrapedAt >= referenceSince).Min(ph => (decimal?)ph.Price),
            }).ToListAsync(cancellationToken);

        var discountCount = rows.Count(r => r.Latest is not null && r.ReferencePrice is not null && r.Latest < r.ReferencePrice);
        var thirtyDayLowCount = rows.Count(r => r.Latest is not null && r.ThirtyDayLowPrice is not null && r.Latest <= r.ThirtyDayLowPrice);
        var lastScanAt = await db.PriceHistories.MaxAsync(ph => (DateTimeOffset?)ph.ScrapedAt, cancellationToken);

        return new HomepageStatsDto(totalProducts, discountCount, thirtyDayLowCount, lastScanAt);
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

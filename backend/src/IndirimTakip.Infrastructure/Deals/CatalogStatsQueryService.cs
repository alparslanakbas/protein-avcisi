using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// Liste sorgusunun süzgeç/sıralama/sayfalama hattını KULLANMAYAN katalog
/// sorguları: sitemap, ana sayfa ve marka istatistikleri, kategori fiyat
/// özeti, süzgeç seçenekleri.
/// </summary>
/// <remarks>
/// <b>NEDEN AYRI (güvenlik/mimari incelemesi, 26 Eylül).</b> DealsQueryService
/// 1.132 satıra ve 13 genel metoda çıkmıştı; bu depoda iki kez canlıyı
/// düşürmüş bir dosyada, liste hattına hiç girmeyen metotlar yalnızca gürültü.
/// Buradakiler oradan yalnızca bayatlık eşiğini ve satıcı etiketlerini
/// paylaşıyor. TAŞIMA SAF: gövdeler birebir aynı; uçların JSON çıktıları ve
/// rota meta verileri taşıma öncesi ve sonrası karşılaştırıldı.
/// </remarks>
public sealed class CatalogStatsQueryService(AppDbContext db)
{
    // sitemap.xml üretimi için hafif bir liste — DealDto'daki fiyat
    // hesaplarına gerek yok, sadece URL kurmak için Id ve son tarama
    // zamanı (lastmod) yeterli. Donmuş/hayalet ürünler burada da hariç
    // tutuluyor (bkz. StaleThreshold) — aksi halde sitemap, artık site
    // içinde hiçbir yerden linklenmeyen (kategori/marka listelerinde
    // görünmeyen) URL'leri Google'a "tara" diye bildirmeye devam ederdi.
    public async Task<IReadOnlyList<SitemapEntryDto>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        var staleSince = DateTimeOffset.UtcNow.Subtract(DealsQueryService.StaleThreshold);

        // Aynı ürünün ikincil kopyaları sitemap'e GİRMİYOR — Google'a
        // indekslemesi için kopya sayfa bildirmenin anlamı yok (bkz.
        // KopyaUrunHaritasi).
        var kopyalar = await KopyaUrunHaritasi.OlusturAsync(db, cancellationToken);
        var ikincilIdler = kopyalar.Keys.ToList();

        return await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
                && !ikincilIdler.Contains(p.Id)
                && p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault() >= staleSince
            select new SitemapEntryDto(
                p.Id,
                p.Name,
                // <lastmod> için son TARAMA değil, içeriğin son gerçekten
                // değiştiği an. Tarama 6 saatte bir tüm katalogu ölçtüğü için
                // eskiden bütün adresler aynı damgayı taşıyordu ve Google
                // sinyali yok sayıyordu (bkz. Product.ContentUpdatedAt).
                p.ContentUpdatedAt
                    ?? p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault(),
                p.Description != null || p.NutritionJson != null))
            .AsNoTracking()
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
        var staleSince = DateTimeOffset.UtcNow.Subtract(DealsQueryService.StaleThreshold);

        // Donmuş/hayalet ürünleri gizle — bkz. StaleThreshold üzerindeki yorum.
        var activeProducts = (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault() >= staleSince
            select p).AsNoTracking();

        var totalProducts = await activeProducts.CountAsync(cancellationToken);

        // Önceden burada tüm aktif ürünlerin Latest/ReferencePrice/ThirtyDayLowPrice'ı
        // ToListAsync ile .NET tarafına çekilip rows.Count(...) ile bellekte sayılıyordu
        // — ana sayfa her yüklendiğinde ~600 satır ağdan geçiyordu. Artık statsQuery
        // sadece bir IQueryable projeksiyonu (henüz SQL'e çevrilmedi), iki CountAsync
        // çağrısı bunun üzerine kendi WHERE'ini ekleyip sayımı veritabanında yaptırıyor
        // — ağdan sadece iki tamsayı geçiyor.
        var statsQuery = activeProducts.Select(p => new
        {
            Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => (decimal?)ph.Price).FirstOrDefault(),
            ReferencePrice = p.PriceHistories.Where(ph => ph.ScrapedAt >= referenceSince).Max(ph => (decimal?)ph.Price),
            ThirtyDayLowPrice = p.PriceHistories.Where(ph => ph.ScrapedAt >= referenceSince).Min(ph => (decimal?)ph.Price),
        });

        var discountCount = await statsQuery.CountAsync(
            r => r.Latest != null && r.ReferencePrice != null && r.Latest < r.ReferencePrice,
            cancellationToken);
        var thirtyDayLowCount = await statsQuery.CountAsync(
            r => r.Latest != null && r.ThirtyDayLowPrice != null && r.ReferencePrice != null
                 && r.Latest <= r.ThirtyDayLowPrice && r.ThirtyDayLowPrice < r.ReferencePrice,
            cancellationToken);
        var lastScanAt = await db.PriceHistories.MaxAsync(ph => (DateTimeOffset?)ph.ScrapedAt, cancellationToken);

        return new HomepageStatsDto(totalProducts, discountCount, thirtyDayLowCount, lastScanAt);
    }

    // Marka sayfasına özgün, kendi verimize dayanan istatistik bölümü için —
    // GetHomepageStatsAsync'in aynı "sadece iki CountAsync, hiç satır çekme"
    // desende marka filtreli hali. Rakip analizinde marka sayfalarının
    // (bizde ve rakipte) en zayıf halka olduğu görüldü — marka hakkında
    // kopyalanmış bir tarihçe/vizyon metni yerine, sadece bizde olan gerçek
    // veriyi (indirim sıklığı/derinliği) göstermek tercih edildi.
    // category verilirse istatistikler markanın YALNIZCA o kategorideki
    // ürünleriyle hesaplanır — marka × kategori sayfaları bu şekilde kendi
    // verisine kavuşuyor.
    public async Task<BrandStatsDto> GetBrandStatsAsync(
        string brandName, int referenceWindowDays = 30, string? category = null, CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);
        var staleSince = DateTimeOffset.UtcNow.Subtract(DealsQueryService.StaleThreshold);

        var activeProducts = (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && b.Name == brandName
                  && (category == null || p.Category == category)
                  && p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault() >= staleSince
            select p).AsNoTracking();

        var totalProducts = await activeProducts.CountAsync(cancellationToken);

        var statsQuery = activeProducts.Select(p => new
        {
            Latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => (decimal?)ph.Price).FirstOrDefault(),
            ReferencePrice = p.PriceHistories.Where(ph => ph.ScrapedAt >= referenceSince).Max(ph => (decimal?)ph.Price),
            ThirtyDayLowPrice = p.PriceHistories.Where(ph => ph.ScrapedAt >= referenceSince).Min(ph => (decimal?)ph.Price),
        });

        var discountedQuery = statsQuery.Where(r => r.Latest != null && r.ReferencePrice != null && r.Latest < r.ReferencePrice);
        var discountCount = await discountedQuery.CountAsync(cancellationToken);
        var averageDiscountPercent = discountCount > 0
            ? Math.Round(
                await discountedQuery.AverageAsync(r => (double)((r.ReferencePrice!.Value - r.Latest!.Value) / r.ReferencePrice.Value * 100), cancellationToken),
                1)
            : (double?)null;

        var thirtyDayLowCount = await statsQuery.CountAsync(
            r => r.Latest != null && r.ThirtyDayLowPrice != null && r.ReferencePrice != null
                 && r.Latest <= r.ThirtyDayLowPrice && r.ThirtyDayLowPrice < r.ReferencePrice,
            cancellationToken);

        var lastScanAt = totalProducts > 0
            ? await (from p in db.Products
                     join b in db.Brands on p.BrandId equals b.Id
                     where b.Name == brandName
                     from ph in p.PriceHistories
                     select (DateTimeOffset?)ph.ScrapedAt)
                .MaxAsync(cancellationToken)
            : null;

        var averagePrice = totalProducts > 0
            ? await statsQuery.Where(r => r.Latest != null)
                .AverageAsync(r => (decimal?)r.Latest, cancellationToken)
            : null;

        return new BrandStatsDto(
            totalProducts, discountCount, thirtyDayLowCount, averageDiscountPercent, lastScanAt,
            averagePrice is null ? null : Math.Round(averagePrice.Value, 2));
    }

    // Ürün incelemesi sayfası için — aynı kategorideki aktif ürünlerin güncel
    // fiyat ortalaması/aralığı. Sadece skaler agregasyon (AverageAsync/Min/Max),
    // ürün satırları hiç .NET tarafına çekilmiyor (GetHomepageStatsAsync'teki
    // aynı "CountAsync, hiç satır yok" desende).
    public async Task<CategoryPriceStatsDto?> GetCategoryPriceStatsAsync(string category, CancellationToken cancellationToken = default)
    {
        var staleSince = DateTimeOffset.UtcNow.Subtract(DealsQueryService.StaleThreshold);

        var latestPrices = (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && p.Category == category
            let latest = p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).FirstOrDefault()
            where latest != null && latest.ScrapedAt >= staleSince
            select latest.Price);

        var count = await latestPrices.CountAsync(cancellationToken);
        if (count == 0) return null;

        var avg = await latestPrices.AverageAsync(cancellationToken);
        var min = await latestPrices.MinAsync(cancellationToken);
        var max = await latestPrices.MaxAsync(cancellationToken);

        return new CategoryPriceStatsDto(count, Math.Round(avg, 2), min, max);
    }

    public async Task<FilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        // Distinct: aynı ada sahip iki marka kaydı oluşabiliyor. Bu, iki
        // taramanın aynı anda çalışıp ikisinin de "marka yok, oluştur"
        // demesinden kaynaklanıyor (kalıcı çözüm ada benzersiz indeks olurdu).
        // Kullanıcı arayüzünde aynı marka iki çip olarak görünmemeli.
        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => b.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var categories = await db.Products
            .AsNoTracking()
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        // Yalnızca gerçekten bayi ürünü varsa satıcı filtresi anlamlı; hepsi
        // markanın kendi sitesindense filtre gösterilmemeli (arayüz boş
        // listede kutuyu gizliyor). Bayi ADLARI listelenmiyor — filtre iki
        // seçenekli, gerekçe için bkz. DealerSellerLabel.
        var bayiUrunuVar = await db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Seller != null, cancellationToken);

        List<string> sellers = bayiUrunuVar
            ? [DealsQueryService.BrandDirectSellerLabel, DealsQueryService.DealerSellerLabel]
            : [];

        return new FilterOptionsDto(brands, categories, sellers);
    }
}

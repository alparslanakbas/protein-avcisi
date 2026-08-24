using System.Globalization;
using System.Text.RegularExpressions;
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

public partial class DealsQueryService(AppDbContext db)
{
    // Markalar kendi sitelerinde bir ürünün SKU/URL'sini değiştirdiğinde
    // scraper eski kaydı bir daha bulamıyor, PriceHistory eklenmesi duruyor
    // — ama Product kaydı fiyat geçmişini kaybetmemek için veritabanında
    // kalmaya devam ediyor (bkz. ScrapeIngestionService, bilinçli bir karar).
    // Tüm markalar 6 saatte bir tarandığı için bu süreden çok daha uzun
    // (48 saat, ~2 kat güvenlik payı) hiç güncellenmemiş bir ürün gerçekten
    // artık markanın feed'inde yoktur — kullanıcıya "aktif takip ediliyor"
    // gibi görünen ama aslında donmuş bir kart göstermemek için liste/
    // istatistik sorgularından gizleniyor. Veri SİLİNMİYOR: doğrudan ürün
    // linki (GetProductByIdAsync) ve favoriler (GetDealsByIdsAsync) hâlâ
    // erişilebilir — kullanıcı zaten bildiği/favorilediği bir ürünün
    // "artık güncellenmiyor" bilgisini saklamak yanıltıcı olurdu.
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(48);

    private static DealDto MapToDealDto(DealRow row)
    {
        var latest = row.Latest;
        var referencePrice = row.ReferencePrice;
        return new DealDto(
            row.Product.Id, row.Product.Name, row.Product.Url, row.Product.ImageUrl,
            row.Product.Category, row.Product.Size, row.Product.Flavor, row.Product.ServingSizeGrams,
            row.Product.ServingsPerPackage,
            row.Product.Description,
            row.Product.NutritionJson,
            row.Product.ProteinPerServingGrams,
            row.BrandName, latest.Price, referencePrice,
            Math.Round((referencePrice - latest.Price) / referencePrice * 100, 1),
            latest.StoreOldPrice,
            latest.StoreOldPrice is decimal storeOld && storeOld > 0
                ? Math.Round((storeOld - latest.Price) / storeOld * 100, 1)
                : null,
            latest.ScrapedAt,
            latest.Price <= row.ThirtyDayLowPrice && row.ThirtyDayLowPrice < referencePrice);
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
        CancellationToken cancellationToken = default,
        // Arama terimini eşanlamlılarıyla genişletmek, kategori SERBEST
        // olduğunda faydalı ("kreatin" yazan "creatine" ürünlerini de
        // bulsun). Ama kategori zaten sabitlenmişse tam tersi etki yapıyor:
        // hesaplayıcı tablosunda "collagen" araması, "collagen" protein-tozu
        // kategorisinin anahtar kelimelerinden biri olduğu için TÜM protein
        // tozlarını döndürüyordu. O yüzden orada kapatılabiliyor.
        bool expandSearchSynonyms = true)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);
        var staleSince = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

        var query = (
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
            }).AsNoTracking();

        // Donmuş/hayalet ürünleri gizle — bkz. StaleThreshold üzerindeki yorum.
        query = query.Where(r => r.Latest != null && r.Latest.ScrapedAt >= staleSince);

        if (brands is { Length: > 0 })
            query = query.Where(r => brands.Contains(r.BrandName));

        if (categories is { Length: > 0 })
            query = query.Where(r => r.Product.Category != null && categories.Contains(r.Product.Category));

        var searchTerm = search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            // "kreatin" yazınca "creatine" geçen ürünleri de bulsun diye
            // (ve tersi) eşanlamlı terimlerle arama terimini genişletiyoruz
            // — kategori sabitlenmiş çağrılarda bu kapatılıyor, bkz.
            // expandSearchSynonyms üzerindeki açıklama.
            var synonyms = expandSearchSynonyms
                ? ProductAttributeParser.GetSearchSynonyms(searchTerm)
                : [];
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

        // Arama varsa önce ALAKA DÜZEYİNE göre sırala — "magnezyum" araması
        // hem "ProteinOcean Magnezyum" (sadece o bileşen) hem "Kreatin +
        // Magnezyum + Çinko Kompleks" (yan bileşen) gibi ürünleri buluyordu;
        // ikincisi indirim oranına göre üstte çıkıp kullanıcının asıl aradığı
        // ürünü gizleyebiliyordu (kullanıcı geri bildirimi). Öncelik: tam
        // eşleşme > isimle başlama > isimde geçme (bu üçü de kendi içinde en
        // KISA/spesifik isme göre); arama yoksa bu sıralama no-op.
        var relevanceOrdered = !string.IsNullOrEmpty(searchTerm)
            ? query
                .OrderBy(r =>
                    r.Product.Name.ToLower() == searchTerm ? 0
                    : r.Product.Name.ToLower().StartsWith(searchTerm) ? 1
                    : r.Product.Name.ToLower().Contains(searchTerm) ? 2
                    : 3)
                .ThenBy(r => r.Product.Name.Length)
            : query.OrderBy(r => 0);

        // Kullanıcı bir sıralama seçtiyse onu uygula; seçmediyse mağaza
        // kampanyaları görünümünde mağazanın beyan ettiği indirim oranına,
        // diğer görünümlerde bizim doğruladığımız indirim oranına göre sırala.
        // Arama varsa bu hep alaka düzeyinden SONRA (ThenBy) gelen ikincil
        // bir sıralama.
        var orderedQuery = sortBy switch
        {
            "name_asc" => relevanceOrdered.ThenBy(r => r.Product.Name),
            "name_desc" => relevanceOrdered.ThenByDescending(r => r.Product.Name),
            "price_asc" => relevanceOrdered.ThenBy(r => r.Latest!.Price).ThenBy(r => r.Product.Name),
            "price_desc" => relevanceOrdered.ThenByDescending(r => r.Latest!.Price).ThenBy(r => r.Product.Name),
            _ => onlyStoreDiscounted
                ? relevanceOrdered.ThenByDescending(r => (r.Latest!.StoreOldPrice!.Value - r.Latest.Price) / r.Latest.StoreOldPrice.Value).ThenBy(r => r.Product.Name)
                : relevanceOrdered.ThenByDescending(r => (r.ReferencePrice!.Value - r.Latest!.Price) / r.ReferencePrice.Value).ThenBy(r => r.Product.Name),
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
            }).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

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
            }).AsNoTracking().ToListAsync(cancellationToken);

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
        var b1 = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand1.ToLower(), cancellationToken);
        var b2 = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand2.ToLower(), cancellationToken);
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
            }).AsNoTracking().ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Latest is not null)
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => (g.Average(r => r.Latest!.Value), g.Count()));
    }

    // Protein ihtiyacı hesaplayıcısının "servis başı en uygun ürünler"
    // tablosu için. Hesap (paket gramajı ÷ porsiyon = servis sayısı, sonra
    // fiyat ÷ servis) Size alanının metin olarak ayrıştırılmasını
    // gerektirdiği için SQL'e çevrilemiyor — bu yüzden kategoriye ait
    // ürünler belleğe alınıp orada hesaplanıyor, sıralama ve sayfalama da
    // bellekte yapılıyor. İSTEMCİYE SAYFA SAYFA dönüyor: sayfa ilk
    // denemede tüm kategoriyi (100 ürün) SSR'a gömüp 451 KB'a çıkmıştı.
    // Yalnızca porsiyon büyüklüğü GERÇEKTEN bilinen ürünler listeleniyor —
    // "30 gr = 1 servis" gibi bir varsayım bu projede hiç yapılmadı.
    public async Task<PagedResult<DealDto>> GetBestValuePerServingAsync(
        string category,
        string[]? brands,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Filtreleme (marka/arama) DB tarafında yapılıyor — burada sadece
        // servis başı hesap ve ona göre sıralama kalıyor.
        var all = await GetDealsAsync(
            referenceWindowDays: 30,
            brands: brands,
            categories: [category],
            search: search,
            minPrice: null,
            maxPrice: null,
            onlyDiscounted: false,
            onlyStoreDiscounted: false,
            sortBy: null,
            page: 1,
            pageSize: 500,
            cancellationToken,
            // Kategori zaten sabit — eşanlamlı genişletme burada aramayı
            // işlevsiz kılıyordu (bkz. parametrenin kendi açıklaması).
            expandSearchSynonyms: false);

        var ranked = all.Items
            .Select(deal => new { Deal = deal, Servings = CalculateServings(deal) })
            // Bir paketten tek servis bile çıkmıyorsa veri tutarsız demektir.
            .Where(x => x.Servings is >= 1)
            .OrderBy(x => x.Deal.CurrentPrice / x.Servings!.Value)
            .Select(x => x.Deal)
            .ToList();

        var totalCount = ranked.Count;
        var items = ranked
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // TotalPages, PagedResult'ın kendi hesapladığı bir özellik.
        return new PagedResult<DealDto>(items, totalCount, page, pageSize);
    }

    // Marka × kategori kesişim sayfaları (/marka/:brand/:category) için —
    // yalnızca GERÇEKTEN ürünü olan çiftler. Boş bir kombinasyon için sayfa
    // üretmek (ör. bir markanın hiç satmadığı kategori) tam da Google'ın
    // "ince içerik" sayarak indekslemediği şey olurdu; sitemap ve iç
    // linkler bu listeye göre kuruluyor.
    public async Task<IReadOnlyList<BrandCategoryPairDto>> GetBrandCategoryPairsAsync(
        CancellationToken cancellationToken = default)
    {
        var staleSince = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

        var rows = await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive
                  && p.Category != null
                  && p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault() >= staleSince
            group p by new { BrandName = b.Name, Category = p.Category! } into g
            select new BrandCategoryPairDto(g.Key.BrandName, g.Key.Category, g.Count()))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.BrandName)
            .ThenByDescending(r => r.ProductCount)
            .ToList();
    }

    // Hesaplayıcı sayfasındaki marka çipleri için — o kategoride servis
    // başı fiyatı GERÇEKTEN hesaplanabilen ürünü olan markalar. Genel
    // /api/filters listesini kullanmak yanıltıcı olurdu: bir markanın o
    // kategoride ürünü olsa bile porsiyon verisi yoksa çipi tıklandığında
    // tablo boş gelirdi.
    public async Task<IReadOnlyList<string>> GetBestValueBrandsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        var all = await GetBestValuePerServingAsync(category, null, null, 1, 500, cancellationToken);
        return all.Items
            .Select(d => d.BrandName)
            .Distinct()
            .OrderBy(b => b)
            .ToList();
    }

    // Bir paketten kaç servis çıktığı. İki kaynak var, öncelik sırasıyla:
    // (1) markanın DOĞRUDAN beyan ettiği servis sayısı (ProteinOcean'ın
    // variant "Servis" özelliği) — türetilmiş değil, en güvenilir kaynak;
    // (2) paket gramajı ÷ porsiyon büyüklüğü (diğer üç marka). İkisi de
    // yoksa null döner ve ürün servis başı fiyat listesine hiç girmez.
    public static decimal? CalculateServings(DealDto deal)
    {
        if (deal.ServingsPerPackage is > 0)
            return deal.ServingsPerPackage.Value;

        var packageGrams = ParsePackageGrams(deal.Size);
        if (packageGrams is > 0 && deal.ServingSizeGrams is > 0)
            return packageGrams.Value / deal.ServingSizeGrams.Value;

        return null;
    }

    // "900 Gr" / "2 Kg" gibi metinleri grama çevirir. Kapsül/adet/ml gibi
    // birimlerde servis başı gram hesabı anlamsız olurdu — null dönüp o
    // ürünler listeye hiç girmiyor.
    private static decimal? ParsePackageGrams(string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
            return null;

        var match = PackageSizeRegex().Match(size.Trim());
        if (!match.Success)
            return null;

        if (!decimal.TryParse(
                match.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value) || value <= 0)
        {
            return null;
        }

        return match.Groups["unit"].Value.Equals("kg", StringComparison.OrdinalIgnoreCase)
            ? value * 1000
            : value;
    }

    [GeneratedRegex(@"^(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>gr|kg)$", RegexOptions.IgnoreCase)]
    private static partial Regex PackageSizeRegex();

    // sitemap.xml üretimi için hafif bir liste — DealDto'daki fiyat
    // hesaplarına gerek yok, sadece URL kurmak için Id ve son tarama
    // zamanı (lastmod) yeterli. Donmuş/hayalet ürünler burada da hariç
    // tutuluyor (bkz. StaleThreshold) — aksi halde sitemap, artık site
    // içinde hiçbir yerden linklenmeyen (kategori/marka listelerinde
    // görünmeyen) URL'leri Google'a "tara" diye bildirmeye devam ederdi.
    public async Task<IReadOnlyList<SitemapEntryDto>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        var staleSince = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

        return await (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault() >= staleSince
            select new SitemapEntryDto(
                p.Id,
                p.Name,
                p.PriceHistories.OrderByDescending(ph => ph.ScrapedAt).Select(ph => ph.ScrapedAt).FirstOrDefault(),
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
        var staleSince = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

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
    public async Task<BrandStatsDto> GetBrandStatsAsync(string brandName, int referenceWindowDays = 30, CancellationToken cancellationToken = default)
    {
        var referenceSince = DateTimeOffset.UtcNow.AddDays(-referenceWindowDays);
        var staleSince = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

        var activeProducts = (
            from p in db.Products
            join b in db.Brands on p.BrandId equals b.Id
            where b.IsActive && b.Name == brandName
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

        return new BrandStatsDto(totalProducts, discountCount, thirtyDayLowCount, averageDiscountPercent, lastScanAt);
    }

    // Ürün incelemesi sayfası için — aynı kategorideki aktif ürünlerin güncel
    // fiyat ortalaması/aralığı. Sadece skaler agregasyon (AverageAsync/Min/Max),
    // ürün satırları hiç .NET tarafına çekilmiyor (GetHomepageStatsAsync'teki
    // aynı "CountAsync, hiç satır yok" desende).
    public async Task<CategoryPriceStatsDto?> GetCategoryPriceStatsAsync(string category, CancellationToken cancellationToken = default)
    {
        var staleSince = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

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
        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => b.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var categories = await db.Products
            .AsNoTracking()
            .Where(p => p.Category != null)
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return new FilterOptionsDto(brands, categories);
    }
}

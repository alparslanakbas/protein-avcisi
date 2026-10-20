using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IndirimTakip.Infrastructure.Deals;

// Marka karşılaştırma sayfaları (/karsilastir/x-vs-y). Statik/elle yazılan bir içerik
// değil, her istekte canlı veriden hesaplanıyor; yeni marka/ürün eklendikçe güncel kalır.
//
// 20 Ekim: sayfa yalnız kategori başına HAM fiyat ortalaması gösteriyordu. Ortalama
// paket boyutuna bakmıyor: 300 g'lık ürünleri çok olan marka, 5 kg kova satan rakibinden
// "ucuz" görünebiliyordu. Asıl ölçüt artık kilogram fiyatı medyanı; /hangi-takviye ile
// aynı kurallar (ValuePickRanker.Eligible: gramaj aralığı, pirinç unu/paket/birim hatası
// korumaları). Kapsül/tablet kategorilerinde kilogram fiyatı yok, ortalama kalıyor.
// Ayrıca son 30 günün fiyat hareketi: iki markayı birbirinden gerçekten ayıran, yalnız
// bizim fiyat geçmişimizde olan veri (ölçüm 20 Ekim: BigJoy 248 düşüş/345 artış, West
// Nutrition ve Torq hiç değişmemiş).
public partial class DealsQueryService
{
    private const int FiyatHareketiGun = 30;

    // Kilogram fiyatının ürünü anlattığı toz kategorileri. Vitamin, L-karnitin, yağ yakıcı
    // dozla satılıyor; atıştırmalıkta fıstık ezmesi ile makarna, enerji ürünlerinde jel ile
    // içecek tozu yan yana gelir (20 Ekim, gerçek çiftlerde görüldü): orada kilogram fiyatı
    // karşılaştırma değil, gürültü. Bu kategorilerde yalnız ortalama, kazanan yok.
    internal static readonly HashSet<string> KgKarsilastirilabilir = new(StringComparer.Ordinal)
    {
        "protein-tozu", "kreatin", "kilo-hacim", "amino-asitler", "pre-workout",
    };

    // Parametre ToLowerInvariant: sol taraf SQL'de lower(); C#'ın ToLower()'ı sunucu kültürü
    // tr-TR ise "HIQ" → "hıq" yapıyor ve marka bulunamıyordu (20 Ekim, yerelde 404; canlı
    // konteynerin kültürü Türkçe olmadığı için görünmüyordu).
    public async Task<BrandComparisonDto?> GetBrandComparisonAsync(string brand1, string brand2, CancellationToken cancellationToken = default)
    {
        var b1 = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand1.ToLowerInvariant(), cancellationToken);
        var b2 = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand2.ToLowerInvariant(), cancellationToken);
        if (b1 is null || b2 is null || b1.Id == b2.Id)
            return null;

        // Liste ve istatistik sorgularıyla aynı taban: 48 saattir güncellenmeyen (donmuş)
        // ürün hesaba girmiyor (bkz. StaleThreshold). Fiyat özetteki güncel fiyat.
        var bayatlikSiniri = DateTimeOffset.UtcNow.Subtract(StaleThreshold);
        var satirlar = await db.Products
            .Where(p => (p.BrandId == b1.Id || p.BrandId == b2.Id)
                        && p.Category != null
                        && p.LatestPrice != null && p.LatestPrice > 0
                        && p.LatestScrapedAt != null && p.LatestScrapedAt >= bayatlikSiniri)
            .Select(p => new { p.Id, p.BrandId, Category = p.Category!, p.Name, p.Size, Price = p.LatestPrice!.Value, p.InStock })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var categories = satirlar
            .Select(r => r.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .Select(kategori =>
            {
                var s1 = Ozet(b1.Id, b1.Name, kategori);
                var s2 = Ozet(b2.Id, b2.Name, kategori);
                return new CategoryComparisonDto(
                    kategori,
                    s1.Ortalama, s1.Adet, s2.Ortalama, s2.Adet,
                    s1.KgMedyan, s1.KgAdet, s1.EnUygun,
                    s2.KgMedyan, s2.KgAdet, s2.EnUygun);
            })
            .ToList();

        var total1 = await db.Products.CountAsync(p => p.BrandId == b1.Id, cancellationToken);
        var total2 = await db.Products.CountAsync(p => p.BrandId == b2.Id, cancellationToken);

        var hareket = await FiyatHareketiAsync([b1.Id, b2.Id], cancellationToken);
        PriceActivityDto Hareket(int markaId) => hareket.GetValueOrDefault(markaId, new PriceActivityDto(0, 0, 0));

        return new BrandComparisonDto(
            b1.Name, b2.Name, total1, total2, categories, Hareket(b1.Id), Hareket(b2.Id));

        (decimal? Ortalama, int Adet, decimal? KgMedyan, int KgAdet, ComparisonPickDto? EnUygun) Ozet(
            int markaId, string markaAdi, string kategori)
        {
            var urunler = satirlar.Where(r => r.BrandId == markaId && r.Category == kategori).ToList();
            if (urunler.Count == 0)
                return (null, 0, null, 0, null);

            var kg = KgKarsilastirilabilir.Contains(kategori)
                ? ValuePickRanker.Eligible(
                    urunler.Select(r => new ValuePickCandidate(r.Id, r.BrandId, markaAdi, r.Name, r.Size, r.Price, r.InStock)),
                    kategori)
                : [];
            var enUygun = kg.Count > 0
                ? new ComparisonPickDto(kg[0].Candidate.ProductId, kg[0].Candidate.ProductName, kg[0].Candidate.Size,
                    kg[0].Candidate.Price, kg[0].PricePerKg)
                : null;

            return (Math.Round(urunler.Average(r => r.Price), 2), urunler.Count,
                Medyan(kg.Select(k => k.PricePerKg).ToList()), kg.Count, enUygun);
        }
    }

    private async Task<Dictionary<int, PriceActivityDto>> FiyatHareketiAsync(int[] markaIdleri, CancellationToken cancellationToken)
    {
        var satirlar = await db.Database.SqlQueryRaw<FiyatHareketiSatiri>(
                FiyatHareketiSorgusu,
                new NpgsqlParameter("markalar", markaIdleri),
                new NpgsqlParameter("baslangic", DateTimeOffset.UtcNow.AddDays(-FiyatHareketiGun)))
            .ToListAsync(cancellationToken);
        return satirlar.ToDictionary(h => h.BrandId, h => new PriceActivityDto(h.Drops, h.Rises, h.ChangedProducts));
    }

    /// <summary>
    /// Marka sayfası (/marka/x/indirim-kodu ve marka × kategori) için yalnız bizde olan veri:
    /// son 30 günün fiyat hareketi ve her toz kategoride markanın kilogram fiyatı medyanının
    /// sitedeki bütün markaların medyanına göre konumu. "Bütün markalar" aynı kurallarla
    /// (ValuePickRanker.Eligible) hesaplanıyor; bayi kopyaları da içinde, liste sorgularıyla aynı taban.
    /// </summary>
    public async Task<BrandInsightsDto?> GetBrandInsightsAsync(string brand, CancellationToken cancellationToken = default)
    {
        var marka = await db.Brands.AsNoTracking()
            .FirstOrDefaultAsync(b => b.IsActive && b.Name.ToLower() == brand.ToLowerInvariant(), cancellationToken);
        if (marka is null)
            return null;

        var bayatlikSiniri = DateTimeOffset.UtcNow.Subtract(StaleThreshold);
        var kategoriler = KgKarsilastirilabilir.ToArray();
        var satirlar = await (
                from p in db.Products
                join b in db.Brands on p.BrandId equals b.Id
                where b.IsActive
                      && p.Category != null && kategoriler.Contains(p.Category)
                      && p.LatestPrice != null && p.LatestPrice > 0
                      && p.LatestScrapedAt != null && p.LatestScrapedAt >= bayatlikSiniri
                select new { p.Id, p.BrandId, BrandName = b.Name, Category = p.Category!, p.Name, p.Size, Price = p.LatestPrice!.Value, p.InStock })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var konumlar = new List<BrandKgPositionDto>();
        foreach (var kategori in kategoriler.Order(StringComparer.Ordinal))
        {
            var adaylar = satirlar
                .Where(r => r.Category == kategori)
                .Select(r => new ValuePickCandidate(r.Id, r.BrandId, r.BrandName, r.Name, r.Size, r.Price, r.InStock))
                .ToList();
            var piyasa = ValuePickRanker.Eligible(adaylar, kategori);
            var markaninki = piyasa.Where(x => x.Candidate.BrandId == marka.Id).ToList();
            if (markaninki.Count == 0)
                continue;

            var enUygun = markaninki[0];
            konumlar.Add(new BrandKgPositionDto(
                kategori,
                Medyan(markaninki.Select(x => x.PricePerKg).ToList()), markaninki.Count,
                Medyan(piyasa.Select(x => x.PricePerKg).ToList()), piyasa.Count,
                piyasa.Select(x => x.Candidate.BrandId).Distinct().Count(),
                new ComparisonPickDto(enUygun.Candidate.ProductId, enUygun.Candidate.ProductName, enUygun.Candidate.Size,
                    enUygun.Candidate.Price, enUygun.PricePerKg)));
        }

        var hareket = await FiyatHareketiAsync([marka.Id], cancellationToken);
        return new BrandInsightsDto(marka.Name, hareket.GetValueOrDefault(marka.Id, new PriceActivityDto(0, 0, 0)), konumlar);
    }

    /// <summary>Sıralı ya da sırasız listenin medyanı (çift sayıda ise ortadaki ikisinin ortalaması).</summary>
    internal static decimal? Medyan(IReadOnlyList<decimal> degerler)
    {
        if (degerler.Count == 0)
            return null;

        var sirali = degerler.Order().ToList();
        var orta = sirali.Count / 2;
        var medyan = sirali.Count % 2 == 1 ? sirali[orta] : (sirali[orta - 1] + sirali[orta]) / 2m;
        return Math.Round(medyan, 2);
    }

    // Ardışık iki tarama arasındaki her fiyat değişimi bir hareket. Pencerenin ilk
    // kaydının öncesi yok (lag NULL), sayılmıyor. Ham SQL global sorgu filtresini
    // atladığı için IsActive açıkça yazılı.
    private const string FiyatHareketiSorgusu = """
        SELECT h."BrandId",
               (count(*) FILTER (WHERE h."Price" < h.onceki))::int AS "Drops",
               (count(*) FILTER (WHERE h."Price" > h.onceki))::int AS "Rises",
               (count(DISTINCT h."ProductId") FILTER (WHERE h."Price" <> h.onceki))::int AS "ChangedProducts"
        FROM (
            SELECT p."BrandId", ph."ProductId", ph."Price",
                   lag(ph."Price") OVER (PARTITION BY ph."ProductId" ORDER BY ph."ScrapedAt") AS onceki
            FROM "PriceHistories" ph
            JOIN "Products" p ON p."Id" = ph."ProductId"
            WHERE p."BrandId" = ANY(@markalar) AND p."IsActive" AND ph."ScrapedAt" >= @baslangic
        ) h
        GROUP BY h."BrandId"
        """;

    internal sealed class FiyatHareketiSatiri
    {
        public int BrandId { get; set; }
        public int Drops { get; set; }
        public int Rises { get; set; }
        public int ChangedProducts { get; set; }
    }
}

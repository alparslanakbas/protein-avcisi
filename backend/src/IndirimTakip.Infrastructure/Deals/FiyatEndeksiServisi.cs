using System.Globalization;
using System.Text;
using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>Bir ayın bir kategorisi. Endeks taban aydan zincirleme; zincir kopmuşsa null.</summary>
public sealed record FiyatEndeksiKategorisi(
    string Kategori,
    decimal? Endeks,
    decimal AylikDegisimYuzde,
    int UrunSayisi,
    int MarkaSayisi,
    decimal ArtanPayi,
    decimal AzalanPayi,
    int Dislanan);

public sealed record FiyatEndeksiAyi(DateOnly Ay, bool Gecici, DateTimeOffset Hesaplandi, IReadOnlyList<FiyatEndeksiKategorisi> Kategoriler);

/// <summary>Aylar en yeniden eskiye.</summary>
public sealed record FiyatEndeksi(DateOnly TabanAy, IReadOnlyList<FiyatEndeksiAyi> Aylar);

/// <summary>
/// Aylık takviye fiyat endeksi (/fiyat-endeksi): aynı ürünlerin bu ayki tipik fiyatı bir önceki aya göre ne kadar
/// değişti. Taban Eylül 2026 = 100, aylar zincirleme bağlanıyor.
/// </summary>
/// <remarks>
/// <b>Yöntem (21 Ekim'de canlı veride ölçülerek seçildi):</b>
/// - Ürünün günlük fiyatı o günün (Türkiye saati) en düşük gözlemi, aylık fiyatı günlük fiyatlarının medyanı. Tek
///   günlük kampanya ya da okuma ayın fiyatını çekmiyor; ay boyu süren indirim çekiyor (rafta ödenen fiyat bu).
/// - Bir ürün iki ayın da en az <see cref="EnAzGun"/> farklı gününde görüldüyse sayılıyor (eşleşen ürün sepeti:
///   kataloğa giren ya da çıkan ürün endeksi oynatmıyor).
/// - Kategori değeri ürün oranlarının geometrik ortalaması (Jevons; TÜİK'in de temel birim endeks yöntemi). Medyan
///   fiyatı değişmeyen çok ürün yüzünden sıfıra yapışıyor (7 Ekim ölçümü).
/// - Ürün başına eşit ağırlık. Önce marka içinde sonra markalar arasında ortalama da ölçüldü: protein tozunda %1,41'e
///   karşı %1,69, en büyük markanın payı %10-19. Fark küçük, ürün başına ortalama anlatması kolay ve standart.
/// - Oranı üç kattan büyük değişen ürün dışarıda (paket değişmiş ya da okuma hatası olabilir) ve sayısı yazılıyor.
///   Kesim bilerek geniş: 6 Eylül'de %53-60'lık bir düşüş gerçek mağaza kampanyası çıkmıştı (KampanyaIndirimServisi).
/// - Bir kategori en az <see cref="EnAzUrun"/> ürün ve <see cref="EnAzMarka"/> markayla yayınlanıyor.
/// - Taban Eylül: Ağustos'ta kategori başına 4 marka vardı ve tek marka sepetin yarısından fazlasıydı (kaynakların
///   çoğu Eylül'de eklendi); Ağustos'tan Eylül'e değişim piyasayı değil o markayı ölçerdi.
/// </remarks>
public sealed class FiyatEndeksiServisi(AppDbContext db, ILogger<FiyatEndeksiServisi> logger)
{
    public static readonly DateOnly TabanAy = new(2026, 9, 1);
    public const int EnAzGun = 7;
    public const int EnAzUrun = 30;
    public const int EnAzMarka = 5;
    public const double OranAlt = 1.0 / 3;
    public const double OranUst = 3;

    // Türkiye 2016'dan beri yaz saati uygulamıyor; sabit UTC+3.
    private static readonly TimeSpan TurkiyeSaati = TimeSpan.FromHours(3);

    // SqlQueryRaw sütunları özellik adına bağlıyor; adlar tırnaklı takma adlarla aynı.
    private sealed record Satir(string Kategori, double? Oran, long Urun, long Marka, long Artan, long Azalan, long Dislanan);

    private const string Sorgu = """
        WITH gunluk AS (
            SELECT ph."ProductId", (ph."ScrapedAt" AT TIME ZONE 'Europe/Istanbul')::date AS gun, MIN(ph."Price") AS fiyat
            FROM "PriceHistories" ph
            WHERE ph."ScrapedAt" >= @baslangic AND ph."ScrapedAt" < @bitis AND ph."Price" > 0
            GROUP BY 1, 2
        ),
        aylik AS (
            SELECT "ProductId", date_trunc('month', gun)::date AS ay, COUNT(*) AS gun_sayisi,
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY fiyat) AS fiyat
            FROM gunluk
            GROUP BY 1, 2
        ),
        oranlar AS (
            SELECT b."ProductId", b.fiyat / a.fiyat AS oran
            FROM aylik a
            JOIN aylik b ON b."ProductId" = a."ProductId"
            WHERE a.ay = @oncekiAy AND b.ay = @ay AND a.gun_sayisi >= @enAzGun AND b.gun_sayisi >= @enAzGun
        ),
        etiketli AS (
            SELECT o.oran, p."Category" AS kategori, p."BrandId" AS marka, o.oran BETWEEN @alt AND @ust AS tutulan
            FROM oranlar o
            JOIN "Products" p ON p."Id" = o."ProductId"
            WHERE p."Category" IS NOT NULL
        )
        SELECT COALESCE(kategori, @tum) AS "Kategori",
               exp(avg(ln(oran)) FILTER (WHERE tutulan)) AS "Oran",
               COUNT(*) FILTER (WHERE tutulan) AS "Urun",
               COUNT(DISTINCT marka) FILTER (WHERE tutulan) AS "Marka",
               COUNT(*) FILTER (WHERE tutulan AND oran > 1.001) AS "Artan",
               COUNT(*) FILTER (WHERE tutulan AND oran < 0.999) AS "Azalan",
               COUNT(*) FILTER (WHERE NOT tutulan) AS "Dislanan"
        FROM etiketli
        GROUP BY GROUPING SETS ((kategori), ())
        """;

    /// <summary>
    /// Kesinleşmemiş ayları hesaplayıp yazıyor: biten ay bir kez (kesin), içinde bulunulan ay her çağrıda (geçici).
    /// </summary>
    public async Task<int> TazeleAsync(DateTimeOffset simdi, CancellationToken ct = default)
    {
        var buAy = AyBasi(simdi);
        var kesinler = await db.PriceIndexMonths.Where(p => !p.IsProvisional)
            .Select(p => p.Month).Distinct().ToListAsync(ct);
        var yazilan = 0;

        for (var ay = TabanAy.AddMonths(1); ay <= buAy; ay = ay.AddMonths(1))
        {
            if (kesinler.Contains(ay))
                continue;

            var satirlar = await HesaplaAsync(ay, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.PriceIndexMonths.Where(p => p.Month == ay).ExecuteDeleteAsync(ct);
            foreach (var s in satirlar)
            {
                db.PriceIndexMonths.Add(new PriceIndexMonth
                {
                    Month = ay,
                    Category = s.Kategori,
                    Ratio = (decimal)s.Oran!.Value,
                    ProductCount = (int)s.Urun,
                    BrandCount = (int)s.Marka,
                    RisenCount = (int)s.Artan,
                    FallenCount = (int)s.Azalan,
                    ExcludedCount = (int)s.Dislanan,
                    IsProvisional = ay == buAy,
                    ComputedAt = simdi.ToUniversalTime(),
                });
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            yazilan += satirlar.Count;
        }

        logger.LogInformation("Fiyat endeksi tazelendi: {Satir} satır.", yazilan);
        return yazilan;
    }

    private async Task<List<Satir>> HesaplaAsync(DateOnly ay, CancellationToken ct)
    {
        var oncekiAy = ay.AddMonths(-1);
        var satirlar = await db.Database.SqlQueryRaw<Satir>(
                Sorgu,
                new NpgsqlParameter("baslangic", AyBasiUtc(oncekiAy)),
                new NpgsqlParameter("bitis", AyBasiUtc(ay.AddMonths(1))),
                new NpgsqlParameter("oncekiAy", oncekiAy),
                new NpgsqlParameter("ay", ay),
                new NpgsqlParameter("enAzGun", EnAzGun),
                new NpgsqlParameter("alt", OranAlt),
                new NpgsqlParameter("ust", OranUst),
                new NpgsqlParameter("tum", PriceIndexMonth.AllCategories))
            .ToListAsync(ct);
        return satirlar.Where(s => s.Oran is not null && s.Urun >= EnAzUrun && s.Marka >= EnAzMarka).ToList();
    }

    public async Task<FiyatEndeksi> OkuAsync(CancellationToken ct = default) =>
        Olustur(await db.PriceIndexMonths.AsNoTracking().ToListAsync(ct));

    /// <summary>
    /// Satırlardan sayfanın gördüğü yapıyı kuruyor. Endeks taban aydan bu aya kadar her ay hesaplanmışsa var; arada
    /// eksik ay olan kategoride zincir kopuyor ve endeks null (aylık değişim yine gösteriliyor).
    /// </summary>
    internal static FiyatEndeksi Olustur(IEnumerable<PriceIndexMonth> satirlar)
    {
        var zincir = new Dictionary<string, (DateOnly Ay, decimal Endeks)>();
        var aylar = new List<FiyatEndeksiAyi>();

        foreach (var grup in satirlar.GroupBy(s => s.Month).OrderBy(g => g.Key))
        {
            var oncekiAy = grup.Key.AddMonths(-1);
            var kategoriler = new List<FiyatEndeksiKategorisi>();
            foreach (var s in grup.OrderByDescending(s => s.Category == PriceIndexMonth.AllCategories)
                         .ThenByDescending(s => s.ProductCount))
            {
                decimal? endeks = oncekiAy == TabanAy
                    ? 100m * s.Ratio
                    : zincir.TryGetValue(s.Category, out var onceki) && onceki.Ay == oncekiAy
                        ? onceki.Endeks * s.Ratio
                        : null;
                if (endeks is { } deger)
                    zincir[s.Category] = (grup.Key, deger);

                kategoriler.Add(new FiyatEndeksiKategorisi(
                    s.Category,
                    endeks is { } e ? Math.Round(e, 1) : null,
                    Math.Round((s.Ratio - 1) * 100, 2),
                    s.ProductCount,
                    s.BrandCount,
                    Pay(s.RisenCount, s.ProductCount),
                    Pay(s.FallenCount, s.ProductCount),
                    s.ExcludedCount));
            }
            aylar.Add(new FiyatEndeksiAyi(grup.Key, grup.Any(s => s.IsProvisional), grup.Max(s => s.ComputedAt), kategoriler));
        }

        aylar.Reverse();
        return new FiyatEndeksi(TabanAy, aylar);
    }

    /// <summary>İndirilebilir veri (CC BY 4.0): sayılar noktalı ondalık, bir satır bir ay × kategori.</summary>
    public static string Csv(FiyatEndeksi endeks)
    {
        var sb = new StringBuilder("ay,kategori,endeks_eylul_2026_100,aylik_degisim_yuzde,urun_sayisi,marka_sayisi,artan_payi_yuzde,azalan_payi_yuzde,gecici\n");
        foreach (var ay in endeks.Aylar.Reverse())
        {
            foreach (var k in ay.Kategoriler)
            {
                // Sabit biçim: decimal'ın basamak sayısı hesaba göre değişiyor ("25" / "25.0"), dosya tutarlı kalsın.
                sb.Append(CultureInfo.InvariantCulture, $"{ay.Ay:yyyy-MM},{k.Kategori},{k.Endeks?.ToString("0.0", CultureInfo.InvariantCulture)},");
                sb.Append(CultureInfo.InvariantCulture, $"{k.AylikDegisimYuzde:0.00},{k.UrunSayisi},{k.MarkaSayisi},{k.ArtanPayi:0.0},{k.AzalanPayi:0.0},{(ay.Gecici ? "evet" : "hayir")}\n");
            }
        }
        return sb.ToString();
    }

    private static decimal Pay(int adet, int toplam) => toplam > 0 ? Math.Round(100m * adet / toplam, 1) : 0m;

    internal static DateOnly AyBasi(DateTimeOffset an)
    {
        var tr = an.ToOffset(TurkiyeSaati);
        return new DateOnly(tr.Year, tr.Month, 1);
    }

    private static DateTimeOffset AyBasiUtc(DateOnly ay) =>
        new DateTimeOffset(ay.Year, ay.Month, 1, 0, 0, 0, TurkiyeSaati).ToUniversalTime();
}

using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>Yönetmelik ölçütüyle gerçek indirimde olan bir ürün.</summary>
public sealed record KampanyaIndirimi(DealDto Urun, decimal OncekiEnDusuk, decimal GercekYuzde, DateTimeOffset IndirimBaslangici);

/// <summary>Mağazanın üstü çizili fiyat gösterdiği ürünlerin yönetmelik ölçütüyle dökümü.</summary>
public sealed record KampanyaOzeti(
    DateTimeOffset Olcum,
    int MagazaIndirimDiyor,
    int Gercek,
    int Ucuzlamamis,
    int Kalici,
    int VeriYetersiz,
    IReadOnlyList<KampanyaIndirimi> GercekIndirimler);

/// <summary>
/// Mağazanın üstü çizili fiyat gösterdiği ürünleri Fiyat Etiketi Yönetmeliği'nin ölçütüyle sınıflıyor: indirimden
/// önceki 30 günün en düşük fiyatı (1 Mart 2022'den beri).
/// </summary>
/// <remarks>
/// <b>Sitenin "gerçek indirim" etiketinden farklı ve daha katı.</b> Etiket fiyatı olağan fiyatla (30 günde en az 7
/// günde görülen en yüksek) karşılaştırıyor, yönetmelik indirimden önceki 30 günün EN DÜŞÜĞÜYLE. 7 Ekim'de aynı 720
/// üründen etiket 127'sini, bu ölçüt 32'sini indirimde buldu. Sezon sayfası ve rapor yazısı
/// (rehber/takviye-indirimleri-gercek-mi) bu ölçütü kullanıyor; ikisi aynı sayıyı göstersin diye tek yer burası.
///
/// <b>Kurallar, ölçülerek konuldu (7 Ekim):</b>
/// - İndirimin başlangıcı: bugünkü fiyatın kesintisiz sürdüğü dönemin ilk gözlemi.
/// - Karşılaştırma fiyatı: o başlangıçtan önceki 30 günde EN AZ İKİ FARKLI GÜNDE görülen en düşük fiyat. Tek
///   gözlemlik düşük fiyat çoğu zaman okuma hatası; 78 üründe en düşük fiyat yalnız bir günde görülmüştü.
/// - Yeterli veri: o 30 günün en az 7 farklı gününde gözlem.
/// - Bugünkü fiyat 30 günden uzun süredir aynıysa "kalıcı": üstü çizili fiyat bir aydır indirim değil.
/// - Gerçek: bugünkü fiyat karşılaştırma fiyatından en az %0,5 düşük.
///
/// <b>Büyük fark veri hatası sayılmıyor.</b> İlk sürümde "fiyat karşılaştırma fiyatının iki katından fazlaysa ürün
/// değişmiş olabilir" diye 49 ürün dışarıda bırakılmıştı (hepsi tek kaynak, ör. 399 ↔ 1.399 TL). İncelenince gerçek
/// bir mağaza kampanyası çıktı: 6 Eylül'de 56 ürün aynı anda %53-60 düşmüş, 14 Eylül'de 53'ü eski fiyata dönmüş,
/// kampanya fiyatları 9'la biten elle konmuş fiyatlar, o günlerde kodda fiyatı etkileyen değişiklik yok. Bu ürünler
/// bir ay içinde bugünkünden çok daha ucuza satılmış; yönetmelik ölçütüyle "ucuzlamamış". Kanıtı olmayan bir
/// "hata" varsayımı, mağazanın beyanını haksız yere korumuştu.
/// </remarks>
public sealed class KampanyaIndirimServisi(AppDbContext db, DealsQueryService deals)
{
    public const int PencereGun = 30;
    public const int EnAzVeriGunu = 7;
    public const int EnDusukIcinEnAzGun = 2;
    public const decimal GercekEsigi = 0.995m;
    private static readonly TimeSpan TazelikSiniri = TimeSpan.FromHours(48);

    private const string Sorgu = """
        WITH taze AS (
            SELECT p."Id", p."LatestPrice" AS fiyat
            FROM "Products" p JOIN "Brands" b ON b."Id" = p."BrandId"
            WHERE p."IsActive" AND b."IsActive" AND p."LatestScrapedAt" > now() - @taze
              AND p."LatestPrice" > 0 AND p."LatestStoreOldPrice" > p."LatestPrice"
        ),
        farkli AS (
            SELECT t."Id", max(ph."ScrapedAt") AS son_farkli
            FROM taze t JOIN "PriceHistories" ph ON ph."ProductId" = t."Id" AND ph."Price" <> t.fiyat
            GROUP BY t."Id"
        ),
        kosu AS (
            SELECT t."Id", t.fiyat,
                   (SELECT min(ph."ScrapedAt") FROM "PriceHistories" ph
                    WHERE ph."ProductId" = t."Id" AND ph."ScrapedAt" > coalesce(f.son_farkli, '-infinity')) AS kosu_bas
            FROM taze t LEFT JOIN farkli f ON f."Id" = t."Id"
        ),
        once AS (
            SELECT k."Id", k.fiyat, k.kosu_bas,
                   (SELECT min(x.fiyat) FROM (
                        SELECT ph."Price" AS fiyat FROM "PriceHistories" ph
                        WHERE ph."ProductId" = k."Id" AND ph."ScrapedAt" >= k.kosu_bas - @pencere AND ph."ScrapedAt" < k.kosu_bas
                        GROUP BY ph."Price"
                        HAVING count(DISTINCT (ph."ScrapedAt" AT TIME ZONE 'UTC')::date) >= @enAzGun) x) AS once_min,
                   (SELECT count(DISTINCT (ph."ScrapedAt" AT TIME ZONE 'UTC')::date) FROM "PriceHistories" ph
                    WHERE ph."ProductId" = k."Id" AND ph."ScrapedAt" >= k.kosu_bas - @pencere AND ph."ScrapedAt" < k.kosu_bas) AS once_gun
            FROM kosu k
        )
        SELECT o."Id" AS "UrunId", o.fiyat AS "Fiyat", o.once_min AS "OncekiEnDusuk", o.kosu_bas AS "KosuBaslangici",
               CASE
                   WHEN o.kosu_bas <= now() - @pencere THEN 'kalici'
                   WHEN o.once_gun < @enAzVeri OR o.once_min IS NULL THEN 'yetersiz'
                   WHEN o.fiyat < o.once_min * @esik THEN 'gercek'
                   ELSE 'ucuzlamamis'
               END AS "Sinif"
        FROM once o
        """;

    public async Task<KampanyaOzeti> OzetAsync(CancellationToken cancellationToken = default)
    {
        var satirlar = await db.Database.SqlQueryRaw<KampanyaSatiri>(
                Sorgu,
                new NpgsqlParameter("taze", TazelikSiniri),
                new NpgsqlParameter("pencere", TimeSpan.FromDays(PencereGun)),
                new NpgsqlParameter("enAzGun", EnDusukIcinEnAzGun),
                new NpgsqlParameter("enAzVeri", EnAzVeriGunu),
                new NpgsqlParameter("esik", GercekEsigi))
            .ToListAsync(cancellationToken);

        var gercek = satirlar.Where(s => s.Sinif == "gercek" && s.OncekiEnDusuk > 0).ToList();
        var urunler = (await deals.GetDealsByIdsAsync(gercek.Select(s => s.UrunId).ToList(), cancellationToken: cancellationToken))
            .ToDictionary(d => d.ProductId);

        var liste = gercek
            .Where(s => urunler.ContainsKey(s.UrunId))
            .Select(s => new KampanyaIndirimi(
                urunler[s.UrunId],
                s.OncekiEnDusuk!.Value,
                Math.Round(100 * (s.OncekiEnDusuk.Value - s.Fiyat) / s.OncekiEnDusuk.Value, 1),
                s.KosuBaslangici))
            .OrderByDescending(k => k.GercekYuzde)
            .ToList();

        int Say(string sinif) => satirlar.Count(s => s.Sinif == sinif);
        return new KampanyaOzeti(
            DateTimeOffset.UtcNow, satirlar.Count, gercek.Count, Say("ucuzlamamis"), Say("kalici"), Say("yetersiz"), liste);
    }

    // SqlQueryRaw sütunları özellik adına bağlıyor; adlar SQL'deki tırnaklı takma adlarla aynı.
    internal sealed class KampanyaSatiri
    {
        public int UrunId { get; set; }
        public decimal Fiyat { get; set; }
        public decimal? OncekiEnDusuk { get; set; }
        public DateTimeOffset KosuBaslangici { get; set; }
        public string Sinif { get; set; } = string.Empty;
    }
}

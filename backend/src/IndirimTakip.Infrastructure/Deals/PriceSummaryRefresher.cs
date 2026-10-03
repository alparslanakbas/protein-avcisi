using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// <c>Products</c> üzerindeki fiyat özeti alanlarını yeniden hesaplar.
///
/// <b>NEDEN VAR (3 Eylül'de ölçüldü).</b> <c>/api/deals</c> isteğinin
/// %97,7'si PostgreSQL içinde geçiyordu: COUNT 654 ms + veri sorgusu
/// 1.437 ms, C# tarafı 49 ms. Sebep, sorgunun 2713 ürünün HER BİRİ için
/// <c>PriceHistories</c> üzerinde 6-8 korelasyonlu alt sorgu çalıştırması
/// (son fiyat, 30 günün en yükseği/en düşüğü — üstelik indirim yüzdesi
/// hesabında aynı alt sorgular tekrar tekrar). İndeks
/// (<c>ProductId, ScrapedAt</c>) zaten vardı; sorun tek aramanın maliyeti
/// değil, 2713 kez tekrarlanmasıydı.
///
/// Burada aynı bilgi TEK küme sorgusuyla hesaplanıp ürüne yazılıyor.
///
/// <b>TÜRETİLMİŞ VERİ.</b> Bu alanlar kaynak değil; <c>PriceHistories</c>
/// tek doğru kaynak olmaya devam ediyor. Kolonlar silinse tekrar
/// hesaplanabilir; yanlışlarsa da düzeltilebilir.
///
/// <b>PENCERE SABİT 30 GÜN.</b> <c>GetDealsAsync</c>'in <c>days</c>
/// parametresi 30'dan farklı gelirse sorgu eski canlı hesaba düşüyor — o
/// yol bilinçli olarak duruyor.
///
/// <b>REFERANS FİYAT = OLAĞAN FİYAT (3 Ekim).</b> Pencerede EN AZ
/// <see cref="OlaganGunSayisi"/> FARKLI GÜN görülmüş en yüksek fiyat; güncel
/// fiyat ondan yüksekse (zam) ya da böyle bir fiyat yoksa (yeni ürün) güncel
/// fiyat, yani indirim 0. Önceden düz en yüksekti ve tek taramalık bir sıçrama
/// 30 gün "gerçek indirim" üretiyordu: ana sayfanın öne çıkan fırsatı
/// ProteinOcean Whey Isolate birkaç günlük 7.699 ₺ yüzünden "%70,1" diyordu,
/// gerçekte 2.199 → 2.299 ₺ zam vardı. Canlıda ölçüldü: %25+ indirimli 143
/// TR ürününün 128'i, UK'deki 162 indirimin 157'si bir haftadan kısa görülmüş
/// fiyata dayanıyordu. Eşik kullanıcı kararı ("bir haftalık fiyat").
/// Alan bilerek aynı kaldı (anlamı değişti): liste sorguları NULL referanslı
/// ürünü dışarıda bırakıyor, yeni bir alan geçmişi kısa ürünleri listeden
/// düşürürdü; indirim filtresi, yüzdesi, sıralama ve istatistikler bu alanı
/// okuduğu için hiçbirine dokunmak gerekmedi.
///
/// <b>TAZELİK.</b> Referans fiyat 30 günlük KAYAN pencereden hesaplanıyor,
/// yani yeni tarama olmasa bile eski bir nokta pencereden çıkınca değişir.
/// Bu yüzden her taramadan sonra (6 saatte bir) yeniden hesaplanıyor.
/// Aradaki sapma en fazla bir tarama turu kadar ve yalnızca 30 gün önceki
/// bir noktanın düşmesinden kaynaklanabilir.
/// </summary>
public sealed class PriceSummaryRefresher(AppDbContext db, ILogger<PriceSummaryRefresher> logger)
{
    /// <summary>
    /// Özetin hesaplandığı pencere. <c>GetDealsAsync</c> yalnızca bu değere
    /// eşit bir <c>days</c> için önceden hesaplanmış alanları kullanabilir.
    /// </summary>
    public const int WindowDays = 30;

    /// <summary>
    /// Bir fiyatın "olağan" sayılması için pencerede görülmesi gereken en az
    /// farklı gün (UTC). Kullanıcı kararı: bir hafta.
    /// </summary>
    public const int OlaganGunSayisi = 7;

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Tek deyim, küme tabanlı. Ürün başına döngü YOK — düzeltmeye
        // çalıştığımız sorunun ta kendisi o olurdu.
        //
        // `son` : en güncel fiyat noktası (DISTINCT ON ile ürün başına bir satır)
        // `pencere` : son 30 günün en yüksek/en düşük fiyatı
        // `gunluk` : her fiyatın pencerede görüldüğü farklı gün sayısı
        // `olagan` : en az OlaganGunSayisi gün görülmüş fiyatların en yükseği
        //
        // Penceresi boş ürünlerde (30 gündür taranmayan) referans eskisi gibi
        // NULL kalıyor; fiyat geçmişi HİÇ olmayan ürünlerde de alanlar NULL.
        // Sorgu tarafında bu ürünler zaten eleniyor (bayat/veri yok).
        const string sql = """
            WITH son AS (
                SELECT DISTINCT ON (ph."ProductId")
                       ph."ProductId", ph."Price", ph."StoreOldPrice", ph."ScrapedAt"
                FROM "PriceHistories" ph
                ORDER BY ph."ProductId", ph."ScrapedAt" DESC
            ),
            pencere AS (
                SELECT ph."ProductId",
                       MAX(ph."Price") AS en_yuksek,
                       MIN(ph."Price") AS en_dusuk
                FROM "PriceHistories" ph
                WHERE ph."ScrapedAt" >= @pencereBaslangici
                GROUP BY ph."ProductId"
            ),
            gunluk AS (
                SELECT ph."ProductId", ph."Price",
                       COUNT(DISTINCT (ph."ScrapedAt" AT TIME ZONE 'UTC')::date) AS gun
                FROM "PriceHistories" ph
                WHERE ph."ScrapedAt" >= @pencereBaslangici
                GROUP BY ph."ProductId", ph."Price"
            ),
            olagan AS (
                SELECT g."ProductId", MAX(g."Price") AS fiyat
                FROM gunluk g
                WHERE g.gun >= @olaganGun
                GROUP BY g."ProductId"
            ),
            yeni AS (
                SELECT son."ProductId", son."Price", son."StoreOldPrice", son."ScrapedAt",
                       pencere.en_dusuk,
                       -- GREATEST NULL'u atlıyor: olağan fiyat yoksa güncel fiyat.
                       CASE WHEN pencere."ProductId" IS NULL THEN NULL
                            ELSE GREATEST(olagan.fiyat, son."Price") END AS referans
                FROM son
                LEFT JOIN pencere ON pencere."ProductId" = son."ProductId"
                LEFT JOIN olagan ON olagan."ProductId" = son."ProductId"
            )
            UPDATE "Products" p
            SET "LatestPrice"           = yeni."Price",
                "LatestStoreOldPrice"   = yeni."StoreOldPrice",
                "LatestScrapedAt"       = yeni."ScrapedAt",
                "ReferencePrice30"      = yeni.referans,
                "LowestPrice30"         = yeni.en_dusuk,
                "PriceSummaryUpdatedAt" = @simdi
            FROM yeni
            WHERE p."Id" = yeni."ProductId"
              AND (
                    p."LatestPrice"      IS DISTINCT FROM yeni."Price"
                 OR p."LatestStoreOldPrice" IS DISTINCT FROM yeni."StoreOldPrice"
                 OR p."LatestScrapedAt" IS DISTINCT FROM yeni."ScrapedAt"
                 OR p."ReferencePrice30" IS DISTINCT FROM yeni.referans
                 OR p."LowestPrice30"   IS DISTINCT FROM yeni.en_dusuk
              );
            """;

        var simdi = DateTimeOffset.UtcNow;
        var pencereBaslangici = simdi.AddDays(-WindowDays);

        var etkilenen = await db.Database.ExecuteSqlRawAsync(
            sql,
            [
                new Npgsql.NpgsqlParameter("pencereBaslangici", pencereBaslangici),
                new Npgsql.NpgsqlParameter("olaganGun", OlaganGunSayisi),
                new Npgsql.NpgsqlParameter("simdi", simdi),
            ],
            cancellationToken);

        logger.LogInformation("Fiyat özeti güncellendi: {Adet} ürün değişti.", etkilenen);
        return etkilenen;
    }
}

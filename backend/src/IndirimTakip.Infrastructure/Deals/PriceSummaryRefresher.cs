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
/// <b>TAZELİK.</b> Referans fiyat 30 günlük KAYAN pencerenin en yükseği,
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

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Tek deyim, küme tabanlı. Ürün başına döngü YOK — düzeltmeye
        // çalıştığımız sorunun ta kendisi o olurdu.
        //
        // `son` : en güncel fiyat noktası (DISTINCT ON ile ürün başına bir satır)
        // `pencere` : son 30 günün en yüksek/en düşük fiyatı
        //
        // Fiyat geçmişi HİÇ olmayan ürünlerde alanlar NULL kalıyor; sorgu
        // tarafında bu ürünler zaten eleniyor (bayat/veri yok).
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
            )
            UPDATE "Products" p
            SET "LatestPrice"           = son."Price",
                "LatestStoreOldPrice"   = son."StoreOldPrice",
                "LatestScrapedAt"       = son."ScrapedAt",
                "ReferencePrice30"      = pencere.en_yuksek,
                "LowestPrice30"         = pencere.en_dusuk,
                "PriceSummaryUpdatedAt" = @simdi
            FROM son
            LEFT JOIN pencere ON pencere."ProductId" = son."ProductId"
            WHERE p."Id" = son."ProductId"
              AND (
                    p."LatestPrice"      IS DISTINCT FROM son."Price"
                 OR p."LatestStoreOldPrice" IS DISTINCT FROM son."StoreOldPrice"
                 OR p."LatestScrapedAt" IS DISTINCT FROM son."ScrapedAt"
                 OR p."ReferencePrice30" IS DISTINCT FROM pencere.en_yuksek
                 OR p."LowestPrice30"   IS DISTINCT FROM pencere.en_dusuk
              );
            """;

        var simdi = DateTimeOffset.UtcNow;
        var pencereBaslangici = simdi.AddDays(-WindowDays);

        var etkilenen = await db.Database.ExecuteSqlRawAsync(
            sql,
            [
                new Npgsql.NpgsqlParameter("pencereBaslangici", pencereBaslangici),
                new Npgsql.NpgsqlParameter("simdi", simdi),
            ],
            cancellationToken);

        logger.LogInformation("Fiyat özeti güncellendi: {Adet} ürün değişti.", etkilenen);
        return etkilenen;
    }
}

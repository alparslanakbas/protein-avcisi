using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Markaların kendi sitelerinde gösterdiği yıldız ortalamasını ve puanlayan
/// sayısını ürün sayfasından tazeler.
///
/// Neden marka başına ayrı bir uygulama yok: puan verisi olan markaların
/// hepsi (HIQ/Shopify, Torq/OpenCart, Yeşilmarka/İkas, Hardline/OniksSoft)
/// bu bilgiyi ürün sayfasının schema.org işaretlemesinde aynı alanlarla
/// veriyor. Tek bir indirme + tek bir ayrıştırıcı hepsini çözüyor.
///
/// Neden <c>ProductDetailBackfillService</c>'e eklenmedi: o servis "bir kez
/// doldur, bir daha dokunma" mantığıyla çalışıyor (açıklama ve besin değeri
/// değişmez). Puan ise sürekli değişiyor, düzenli tazelenmesi gerekiyor —
/// farklı bir yaşam döngüsü, bu yüzden ayrı bir damga (RatingCheckedAt) ve
/// ayrı bir servis.
/// </summary>
public class ProductRatingRefreshService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<ProductRatingRefreshService> logger)
{
    // Marka sitelerini yormamak için istekler arası nezaket beklemesi —
    // ProductDetailBackfillService ile aynı değer.
    private static readonly TimeSpan DelayBetweenProducts = TimeSpan.FromMilliseconds(750);

    // Her çalışmada en eski kontrol edilenlerden bu kadarı tazeleniyor.
    // Katalog ~1000 ürün olduğu için tam bir tur birkaç güne yayılıyor;
    // puan günden güne kayda değer değişmediği için bu yeterli.
    private const int MaxProductsPerRun = 80;

    // Tarayıcı User-Agent'ı taşıyan paylaşılan istemci — markaların çoğu
    // (Cloudflare arkasındakiler dahil) UA'sız istekleri reddediyor.
    public const string RatingHttpClientName = "product-rating";

    /// <summary>
    /// Puanı hiç kontrol edilmemiş ya da en uzun süredir kontrol edilmemiş
    /// ürünleri tazeler. Yalnızca puan verisi olan markalarla sınırlamıyoruz:
    /// bugün yorum toplamayan bir marka yarın toplamaya başlarsa kendiliğinden
    /// yakalanır. Damga her denemede yazıldığı için veri bulunmayan ürünler
    /// sırayı tıkamıyor.
    /// </summary>
    public async Task<int> RefreshAsync(int? maxProducts = null, CancellationToken cancellationToken = default)
    {
        var take = maxProducts ?? MaxProductsPerRun;

        var products = await db.Products
            .OrderBy(p => p.RatingCheckedAt ?? DateTimeOffset.MinValue)
            .ThenBy(p => p.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var httpClient = httpClientFactory.CreateClient(RatingHttpClientName);
        var updated = 0;

        foreach (var product in products)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync(product.Url, cancellationToken);
                var (value, count) = AggregateRatingParser.Parse(html);

                // Tek-iki yorumdan gelen "5.0" bir ortalama değil; bu eşiğin
                // altındakini kaydetmiyoruz ki sıralamada gerçek ortalamaların
                // önüne geçmesin.
                if (value is not null && count >= AggregateRatingParser.MinimumMeaningfulRatingCount)
                {
                    product.RatingValue = value;
                    product.RatingCount = count;
                    updated++;
                }
                else
                {
                    // Marka puanı kaldırmış ya da hiç yorum yoksa eski değeri
                    // taşımaya devam etmek yanıltıcı olurdu.
                    product.RatingValue = null;
                    product.RatingCount = null;
                }
            }
            catch (Exception ex)
            {
                // Tek bir ürünün sayfası açılmazsa tur devam etmeli.
                logger.LogWarning(ex, "Puan tazelenemedi: {Url}", product.Url);
            }

            // Damga başarısız denemede de yazılıyor: aksi halde erişilemeyen
            // aynı ürün her turda tekrar denenip sırayı sonsuza dek tıkardı.
            product.RatingCheckedAt = DateTimeOffset.UtcNow;

            await Task.Delay(DelayBetweenProducts, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Puan tazeleme: {Checked} ürün kontrol edildi, {Updated} üründe puan bulundu.",
            products.Count, updated);

        return updated;
    }
}

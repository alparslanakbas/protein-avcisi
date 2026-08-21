using IndirimTakip.Core.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

// Açıklaması ve besin değeri normal taramada gelmeyen markalar (SSN/Hardline/
// ProteinOcean — IProductDetailFetcher implemente ediyorlar) için, ürün başına
// TEK bir HTTP isteğiyle ikisini birden tamamlar. HIQ'ya hiç dokunmuyor
// (Shopify body_html'inde ikisi de normal taramada geliyor).
//
// Bir kez doldurulan açıklama kalıcıdır. Besin değeri için ise NutritionCheckedAt
// damgası kullanılıyor: çoğu üründe (aksesuar, bar, atıştırmalık) gerçekten
// tablo yok, bu damga olmadan aynı ürünler her hafta sonsuza kadar tekrar
// denenirdi.
public class ProductDetailBackfillService(
    AppDbContext db,
    IEnumerable<IBrandScraper> scrapers,
    ILogger<ProductDetailBackfillService> logger)
{
    // Marka sitesini yormamak için ürün istekleri arası nezaket beklemesi.
    private static readonly TimeSpan DelayBetweenProducts = TimeSpan.FromMilliseconds(750);

    // Tek bir çalışmada en fazla bu kadar ürün denenir — 3 markanın tüm
    // eksiklerini tek seferde çekmek yerine kademeli ilerlemek hem bir
    // çalışmanın süresini makul tutar hem de bir sorun çıkarsa etkiyi sınırlar.
    private const int MaxProductsPerRun = 60;

    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var totalUpdated = 0;
        var totalAttempted = 0;

        foreach (var scraper in scrapers.OfType<IProductDetailFetcher>())
        {
            var brandScraper = (IBrandScraper)scraper;
            var remaining = MaxProductsPerRun - totalAttempted;
            if (remaining <= 0)
                break;

            // Açıklaması VEYA besin değeri henüz hiç bakılmamış ürünler.
            // (Açıklama backfill'i daha önce çalıştığı için bir kısmında
            // açıklama dolu ama NutritionCheckedAt null — onlar da hedefte.)
            var missingProducts = await db.Products
                .Where(p => p.Brand!.Name == brandScraper.BrandName
                    && (p.Description == null || p.NutritionCheckedAt == null))
                .Take(remaining)
                .ToListAsync(cancellationToken);

            if (missingProducts.Count == 0)
                continue;

            logger.LogInformation(
                "{Brand}: {Count} üründe eksik detay var, tamamlanıyor.", brandScraper.BrandName, missingProducts.Count);

            foreach (var product in missingProducts)
            {
                totalAttempted++;
                try
                {
                    var details = await scraper.FetchDetailsAsync(product.Url, cancellationToken);

                    // ??= bilinçli: var olan (daha güvenilir) değeri ezmiyor.
                    product.Description ??= details.Description;
                    product.NutritionJson ??= details.NutritionJson;
                    product.ProteinPerServingGrams ??= details.ProteinPerServingGrams;

                    // Açıklama metninde porsiyon büyüklüğü de geçiyor olabilir
                    // ("1 ölçek (30 g)" gibi) — scraper yapısal bir değer
                    // vermediyse buradan çıkarıyoruz.
                    if (details.Description is not null)
                        product.ServingSizeGrams ??= ProductAttributeParser.ExtractServingSizeGrams(details.Description);

                    // Sayfaya başarıyla bakıldı — tablo bulunmuş olsun olmasın
                    // damgalıyoruz ki bir daha sonsuza kadar denenmesin.
                    product.NutritionCheckedAt = DateTimeOffset.UtcNow;
                    totalUpdated++;
                }
                catch (Exception ex)
                {
                    // Tek bir ürünün hatası (404, geçici ağ sorunu vb.) diğerlerini
                    // durdurmasın. Damga da atılmıyor — sonraki çalışmada tekrar denenir.
                    logger.LogWarning(ex, "{Brand} - {Url} detayları çekilemedi.", brandScraper.BrandName, product.Url);
                }

                await Task.Delay(DelayBetweenProducts, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return totalUpdated;
    }
}

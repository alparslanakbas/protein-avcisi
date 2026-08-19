using IndirimTakip.Core.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

// Açıklaması normal taramada gelmeyen markalar (SSN/Hardline/ProteinOcean —
// IProductDescriptionFetcher implemente ediyorlar) için, ürün başına ayrı bir
// HTTP isteğiyle eksik açıklamaları tamamlar. HIQ'ya hiç dokunmuyor (açıklaması
// zaten normal taramada geliyor, IProductDescriptionFetcher implemente etmiyor).
// Bir kez doldurulan açıklama kalıcıdır — sonraki çalışmalar sadece HÂLÂ null
// olan ürünleri hedefler, marka değişmediyse tekrar istek atılmaz.
public class DescriptionBackfillService(
    AppDbContext db,
    IEnumerable<IBrandScraper> scrapers,
    ILogger<DescriptionBackfillService> logger)
{
    // Marka sitesini yormamak için ürün istekleri arası nezaket beklemesi.
    private static readonly TimeSpan DelayBetweenProducts = TimeSpan.FromMilliseconds(750);

    // Tek bir çalışmada en fazla bu kadar ürün denenir — 3 markanın (SSN ~114,
    // Hardline ~182, ProteinOcean ~161) tüm eksiklerini tek seferde çekmek
    // yerine kademeli ilerlemek hem bir çalışmanın süresini makul tutar hem de
    // bir sorun çıkarsa etkiyi sınırlar. Sonraki haftalık çalışmalar kalan
    // eksikleri tamamlamaya devam eder.
    private const int MaxProductsPerRun = 60;

    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var totalUpdated = 0;
        var totalAttempted = 0;

        foreach (var scraper in scrapers.OfType<IProductDescriptionFetcher>())
        {
            var brandScraper = (IBrandScraper)scraper;
            var remaining = MaxProductsPerRun - totalAttempted;
            if (remaining <= 0)
                break;

            var missingProducts = await db.Products
                .Where(p => p.Brand!.Name == brandScraper.BrandName && p.Description == null)
                .Take(remaining)
                .ToListAsync(cancellationToken);

            if (missingProducts.Count == 0)
                continue;

            logger.LogInformation(
                "{Brand}: {Count} ürünün açıklaması eksik, tamamlanıyor.", brandScraper.BrandName, missingProducts.Count);

            foreach (var product in missingProducts)
            {
                totalAttempted++;
                try
                {
                    var description = await scraper.FetchDescriptionAsync(product.Url, cancellationToken);
                    if (description is not null)
                    {
                        product.Description = description;
                        totalUpdated++;
                    }
                }
                catch (Exception ex)
                {
                    // Tek bir ürünün hatası (404, geçici ağ sorunu vb.) diğerlerini durdurmasın.
                    logger.LogWarning(ex, "{Brand} - {Url} açıklaması çekilemedi.", brandScraper.BrandName, product.Url);
                }

                await Task.Delay(DelayBetweenProducts, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return totalUpdated;
    }
}

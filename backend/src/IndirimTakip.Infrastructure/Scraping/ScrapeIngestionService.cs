using IndirimTakip.Core.Entities;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IndirimTakip.Infrastructure.Scraping;

public class ScrapeIngestionService(
    AppDbContext db,
    ProductWatchNotifier watchNotifier,
    IndexNowClient indexNow,
    IConfiguration configuration)
{
    public async Task<int> IngestAsync(IBrandScraper scraper, CancellationToken cancellationToken = default)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Name == scraper.BrandName, cancellationToken);
        if (brand is null)
        {
            brand = new Brand { Name = scraper.BrandName, BaseUrl = scraper.BaseUrl, IsActive = true };
            db.Brands.Add(brand);
        }

        var scrapedProducts = await scraper.ScrapeAsync(cancellationToken);
        var scrapedAt = DateTimeOffset.UtcNow;

        var existingProducts = await db.Products
            .Where(p => p.Brand!.Name == scraper.BrandName)
            .ToDictionaryAsync(p => p.Url, cancellationToken);

        var touchedProducts = new List<Product>();
        // Yalnızca bu taramada İLK KEZ görülen ürünler — IndexNow'a yeni
        // adresleri bildirmek için. Protokol, değişmeyen adresleri tekrar
        // tekrar göndermemeyi şart koşuyor; fiyat değişimi adresi
        // değiştirmediği için burada yalnızca yeni ürünler toplanıyor.
        var newProducts = new List<Product>();

        foreach (var scraped in scrapedProducts)
        {
            // Marka kendi kategorisini vermiyorsa (HIQ/Hardline/ProteinOcean) isimden tahmin et
            // — arama kutusunun markadan bağımsız çalışması buna dayanıyor.
            var category = scraped.Category ?? ProductAttributeParser.InferCategory(scraped.Name);
            var size = ProductAttributeParser.ExtractSize(scraped.Name);
            var flavor = ProductAttributeParser.ExtractFlavor(scraped.Name);

            if (!existingProducts.TryGetValue(scraped.Url, out var product))
            {
                product = new Product
                {
                    Brand = brand,
                    Name = scraped.Name,
                    Url = scraped.Url,
                    ImageUrl = scraped.ImageUrl,
                    Category = category,
                    Size = size,
                    Flavor = flavor,
                    // Porsiyon: önce scraper'ın yapısal olarak verdiği değer
                    // (HIQ'nun besin tablosu — en güvenilir kaynak), o yoksa
                    // markanın açıklama metninden çıkarım.
                    ServingSizeGrams = scraped.ServingSizeGrams
                        ?? ProductAttributeParser.ExtractServingSizeGrams(scraped.Description),
                    ServingsPerPackage = scraped.ServingsPerPackage,
                    Description = scraped.Description,
                    NutritionJson = scraped.NutritionJson,
                    ProteinPerServingGrams = scraped.ProteinPerServingGrams,
                };
                db.Products.Add(product);
                newProducts.Add(product);
            }
            else
            {
                product.Name = scraped.Name;
                product.ImageUrl = scraped.ImageUrl;
                product.Category = category;
                product.Size = size;
                product.Flavor = flavor;
                // Açıklamayı henüz çekmeyen scraper'lar (SSN/Hardline) scraped.Description
                // hiç göndermiyor — bu durumda var olan değeri SIFIRLAMIYORUZ. Açıklama
                // çeken markalarda (HIQ) ise her taramada güncel tutuluyor.
                if (scraped.Description is not null)
                    product.Description = scraped.Description;

                // Porsiyon, Description atamasından SONRA hesaplanıyor — güncel
                // açıklamayı kullanabilmek için. Scraper yapısal bir değer
                // veriyorsa (HIQ) o kazanır, yoksa açıklamadan çıkarılır.
                product.ServingSizeGrams = scraped.ServingSizeGrams
                    ?? ProductAttributeParser.ExtractServingSizeGrams(product.Description);

                // Sadece marka bu bilgiyi veriyorsa güncelle — vermeyen
                // markalarda (SSN/Hardline/HIQ) mevcut değer sıfırlanmasın.
                if (scraped.ServingsPerPackage is not null)
                    product.ServingsPerPackage = scraped.ServingsPerPackage;

                // Besin değeri normal taramada sadece HIQ'dan geliyor; diğer
                // 3 marka için ayrı bir backfill servisi var (Description ile
                // aynı desen — göndermeyen markada mevcut değer korunuyor).
                if (scraped.NutritionJson is not null)
                {
                    product.NutritionJson = scraped.NutritionJson;
                    product.ProteinPerServingGrams = scraped.ProteinPerServingGrams;
                }
            }

            product.PriceHistories.Add(new PriceHistory
            {
                Price = scraped.Price,
                StoreOldPrice = scraped.StoreOldPrice,
                ScrapedAt = scrapedAt,
            });
            touchedProducts.Add(product);
        }

        await db.SaveChangesAsync(cancellationToken);

        // "Haber Ver" bildirimleri — yeni ürünlerin Id'si de ancak
        // SaveChangesAsync sonrası kesinleşiyor, bu yüzden burada.
        await watchNotifier.CheckAndNotifyAsync(touchedProducts.Select(p => p.Id).ToList(), cancellationToken);

        // Yeni ürün adreslerini arama motorlarına bildir. Ürün kimliği de
        // ancak kayıt sonrası kesinleştiği için burada.
        if (newProducts.Count > 0 && indexNow.IsEnabled)
        {
            var frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr").TrimEnd('/');
            var urls = newProducts
                .Select(p => $"{frontendBaseUrl}/urun/{p.Id}/{Slugifier.Slugify(p.Name)}")
                .ToList();
            await indexNow.SubmitAsync(urls, cancellationToken);
        }

        return scrapedProducts.Count;
    }
}

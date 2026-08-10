using IndirimTakip.Core.Entities;
using IndirimTakip.Core.Scraping;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Scraping;

public class ScrapeIngestionService(AppDbContext db)
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

        foreach (var scraped in scrapedProducts)
        {
            if (!existingProducts.TryGetValue(scraped.Url, out var product))
            {
                product = new Product
                {
                    Brand = brand,
                    Name = scraped.Name,
                    Url = scraped.Url,
                    ImageUrl = scraped.ImageUrl,
                    Category = scraped.Category,
                };
                db.Products.Add(product);
            }
            else
            {
                product.Name = scraped.Name;
                product.ImageUrl = scraped.ImageUrl;
                product.Category = scraped.Category;
            }

            product.PriceHistories.Add(new PriceHistory
            {
                Price = scraped.Price,
                ScrapedAt = scrapedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return scrapedProducts.Count;
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Hiq;

public class HiqScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "HIQ";
    public string BaseUrl => "https://takehiq.com";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ScrapedProduct>();

        for (var page = 1; ; page++)
        {
            var response = await httpClient.GetFromJsonAsync<ShopifyProductsResponse>(
                $"products.json?limit=250&page={page}", JsonOptions, cancellationToken);

            if (response is null || response.Products.Count == 0)
                break;

            foreach (var product in response.Products)
            {
                var variant = product.Variants.Find(v => v.Available);
                if (variant is null)
                    continue;

                result.Add(new ScrapedProduct(
                    Name: product.Title,
                    Url: $"https://takehiq.com/products/{product.Handle}",
                    ImageUrl: product.Images.Count > 0 ? product.Images[0].Src : null,
                    Category: string.IsNullOrWhiteSpace(product.ProductType) ? null : product.ProductType,
                    Price: variant.Price));
            }

            if (response.Products.Count < 250)
                break;
        }

        return result;
    }
}

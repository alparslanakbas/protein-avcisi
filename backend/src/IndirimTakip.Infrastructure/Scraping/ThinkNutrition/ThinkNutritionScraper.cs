using System.Net.Http.Json;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Scraping.ThinkNutrition;

// Think Nutrition — yirmi üçüncü kaynak. ikas altyapısı, GNC/Heyday/Imperium
// ile aynı storefront GraphQL sorgusu; farkı yalnızca kimlikler.
//
// ŞİMDİYE KADARKİ EN TEMİZ KATALOG: 23 ürünün tamamı gerçek spor takviyesi
// (whey, pre-workout, kreatin, BCAA, EAA, glutamin, gainer, karnitin,
// termojenik). Aksesuar, gıda ya da çok ürünlü set YOK — bu yüzden GNC'deki
// gibi kategori beyaz listesine ya da Imperium'daki gibi kuru gıda elemesine
// gerek kalmadı. Ortak aksesuar süzgeci yine de güvenlik ağı olarak duruyor.
//
// MARKA ADI: kaynağın `brand` alanı stilize logo biçimini veriyor ("th!nk").
// Sitenin kendi `og:site_name` etiketi "Think Nutrition" diyor ve alan adı da
// öyle; kullanıcının arayacağı ad bu, o yüzden kanonik yazım olarak seçildi.
public class ThinkNutritionScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Think Nutrition";
    public string BaseUrl => "https://thinknutrition.com.tr";

    private const string MerchantId = "0317ca94-64d5-48a0-abbc-a0e8a1a1dc41";
    private const string SalesChannelId = "6336b610-82f4-47a2-9781-e3230c60372b";
    private const string StorefrontId = "1c4e0353-fcab-4c2f-8507-aeb89957fb29";

    // Sitenin kendi frontend'inin gönderdiği public storefront anahtarı —
    // içinde yalnızca merchant/storefront/salesChannel ID'leri var.
    private const string ApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtIjoiMDMxN2NhOTQtNjRkNS00OGEwLWFiYmMtYTBlOGExYTFkYzQxIiwic2YiOiIxYzRlMDM1My1mY2FiLTRjMmYtODUwNy1hZWI4OTk1N2ZiMjkiLCJzZnQiOjEsInNsIjoiNjMzNmI2MTAtODJmNC00N2EyLTk3ODEtZTMyMzBjNjAzNzJiIn0.kw0B6A4NQm_3cG7RJe7b_gqZxXv6iPr6Siyjg73q_hw";

    private const int PageSize = 100;

    private const string SearchProductsQuery = """
        query searchProducts($input: SearchInput!) {
          searchProducts(input: $input) {
            totalCount
            results {
              name
              metaData { slug }
              variants {
                prices { sellPrice }
                images { id fileName isMain }
                stocks { stockCount }
              }
            }
          }
        }
        """;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var products = new List<ScrapedProduct>();
        var receivedCount = 0;
        var page = 1;

        while (true)
        {
            var response = await SearchProductsPageAsync(page, cancellationToken);
            var result = response?.Data?.SearchProducts;
            var results = result?.Results ?? [];
            if (results.Count == 0)
                break;

            foreach (var product in results)
            {
                if (string.IsNullOrEmpty(product.MetaData?.Slug))
                    continue;

                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Name))
                    continue;

                var variant = product.Variants.Find(v => v.Stocks.Sum(s => s.StockCount) > 0)
                    ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Prices.Count == 0)
                    continue;

                var inStock = product.Variants.Any(v => v.Stocks.Sum(s => s.StockCount) > 0);
                var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

                products.Add(new ScrapedProduct(
                    Name: product.Name,
                    Url: $"{BaseUrl}/{product.MetaData.Slug}",
                    ImageUrl: image is null
                        ? null
                        : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
                    // Kaynağın kategorileri bizim slug'larımız değil
                    // ("Whey Protein", "Pre-Workout"); kategori isimden çıkarılıyor.
                    Category: null,
                    Price: variant.Prices[0].SellPrice,
                    InStock: inStock));
            }

            receivedCount += results.Count;
            if (receivedCount >= (result?.TotalCount ?? 0))
                break;

            page++;
        }

        return products;
    }

    private async Task<SearchProductsGraphQlResponse?> SearchProductsPageAsync(int page, CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = SearchProductsQuery,
            variables = new
            {
                input = new
                {
                    locale = "tr",
                    page,
                    perPage = PageSize,
                    filterList = Array.Empty<object>(),
                    facetList = Array.Empty<object>(),
                    salesChannelId = SalesChannelId,
                    query = "",
                    order = new[] { new { direction = "ASC", type = "MANUAL_SORT" } },
                    showStockOption = "SHOW_ALL",
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/sf/graphql?op=searchProducts")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("x-api-key", ApiKey);
        request.Headers.Add("x-sfid", StorefrontId);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchProductsGraphQlResponse>(cancellationToken: cancellationToken);
    }
}

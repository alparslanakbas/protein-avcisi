using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Biofitle;

/// <summary>
/// biofitle.com — İkas public storefront GraphQL kataloğu. Altı ürünün yalnız
/// üçü kaynakta açıkça "Yüksek Proteinli Kahvaltılık Gevrek" olarak geçer;
/// kullanıcı talebiyle bunlar sağlıklı atıştırmalıklar kategorisine alınır.
/// Standart mısır gevreği ve pirinç patlakları kapsam dışıdır.
/// </summary>
public class BiofitleScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Biofit";
    public string BaseUrl => "https://biofitle.com";

    private const string MerchantId = "37e283dd-51a1-43e4-9b9f-e5acaeb63164";
    private const string SalesChannelId = "57571adb-c553-4c6b-8697-9c737356ae71";
    private const string StorefrontId = "ce3d0bd5-a795-4d11-a306-6d11107f521d";
    private const int PageSize = 100;

    // Sitenin kendi frontend'inin kullandığı public storefront JWT'si; yalnız
    // yukarıdaki merchant/storefront/salesChannel kimliklerini içeriyor.
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJtIjoiMzdlMjgzZGQtNTFhMS00M2U0LTliOWYtZTVhY2FlYjYzMTY0Iiwic2YiOiJjZTNkMGJkNS1h" +
        "Nzk1LTRkMTEtYTMwNi02ZDExMTA3ZjUyMWQiLCJzZnQiOjEsInNsIjoiNTc1NzFhZGItYzU1My00YzZi" +
        "LTg2OTctOWM3MzczNTZhZTcxIn0.VJ-NLK4vVm9xlkGyPbv8-nYz5qms6g__FxvCevdfddU";

    private const string SearchProductsQuery = """
        query searchProducts($input: SearchInput!) {
          searchProducts(input: $input) {
            totalCount
            results {
              name
              brand { name }
              metaData { slug }
              categories { name }
              variants {
                prices { sellPrice discountPrice }
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
                var scraped = Convert(product);
                if (scraped is not null)
                    products.Add(scraped);
            }

            receivedCount += results.Count;
            if (receivedCount >= (result?.TotalCount ?? 0))
                break;

            page++;
        }

        return products;
    }

    internal ScrapedProduct? Convert(BiofitleProduct product)
    {
        var slug = product.MetaData?.Slug?.Trim('/');
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(product.Name))
            return null;

        if (!product.Name.Contains("Yüksek Proteinli", StringComparison.OrdinalIgnoreCase)
            || product.Categories?.Any(category => category.Name?.Equals(
                "Kahvaltılık Gevrek",
                StringComparison.OrdinalIgnoreCase) == true) != true)
        {
            return null;
        }

        if (product.Brand?.Name?.Equals("BİOFİT", StringComparison.OrdinalIgnoreCase) != true
            && product.Brand?.Name?.Equals("BIOFIT", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        var variant = product.Variants.Find(candidate => candidate.Stocks.Sum(stock => stock.StockCount) > 0)
            ?? product.Variants.FirstOrDefault();
        var sourcePrice = variant?.Prices.FirstOrDefault();
        if (variant is null || sourcePrice is null)
            return null;

        var currentPrice = sourcePrice.DiscountPrice is > 0m
            && sourcePrice.DiscountPrice < sourcePrice.SellPrice
                ? sourcePrice.DiscountPrice.Value
                : sourcePrice.SellPrice;
        if (currentPrice <= 0m)
            return null;

        var image = variant.Images.Find(candidate => candidate.IsMain) ?? variant.Images.FirstOrDefault();

        return new ScrapedProduct(
            Name: product.Name.Trim(),
            Url: $"{BaseUrl}/{slug}",
            ImageUrl: image is null
                ? null
                : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
            Category: "saglikli-atistirmaliklar",
            Price: currentPrice,
            StoreOldPrice: currentPrice < sourcePrice.SellPrice ? sourcePrice.SellPrice : null,
            InStock: product.Variants.Any(candidate => candidate.Stocks.Sum(stock => stock.StockCount) > 0));
    }

    private async Task<BiofitleGraphQlResponse?> SearchProductsPageAsync(
        int page,
        CancellationToken cancellationToken)
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
        return await response.Content.ReadFromJsonAsync<BiofitleGraphQlResponse>(cancellationToken: cancellationToken);
    }
}

internal sealed class BiofitleGraphQlResponse
{
    [JsonPropertyName("data")]
    public BiofitleSearchData? Data { get; set; }
}

internal sealed class BiofitleSearchData
{
    [JsonPropertyName("searchProducts")]
    public BiofitleSearchResult? SearchProducts { get; set; }
}

internal sealed class BiofitleSearchResult
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("results")]
    public List<BiofitleProduct> Results { get; set; } = [];
}

internal sealed class BiofitleProduct
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("brand")]
    public BiofitleBrand? Brand { get; set; }

    [JsonPropertyName("metaData")]
    public BiofitleMetaData? MetaData { get; set; }

    [JsonPropertyName("categories")]
    public List<BiofitleCategory>? Categories { get; set; }

    [JsonPropertyName("variants")]
    public List<BiofitleVariant> Variants { get; set; } = [];
}

internal sealed class BiofitleBrand
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class BiofitleMetaData
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}

internal sealed class BiofitleCategory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class BiofitleVariant
{
    [JsonPropertyName("prices")]
    public List<BiofitlePrice> Prices { get; set; } = [];

    [JsonPropertyName("images")]
    public List<BiofitleImage> Images { get; set; } = [];

    [JsonPropertyName("stocks")]
    public List<BiofitleStock> Stocks { get; set; } = [];
}

internal sealed class BiofitlePrice
{
    [JsonPropertyName("sellPrice")]
    public decimal SellPrice { get; set; }

    [JsonPropertyName("discountPrice")]
    public decimal? DiscountPrice { get; set; }
}

internal sealed class BiofitleImage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }

    [JsonPropertyName("isMain")]
    public bool IsMain { get; set; }
}

internal sealed class BiofitleStock
{
    [JsonPropertyName("stockCount")]
    public int StockCount { get; set; }
}

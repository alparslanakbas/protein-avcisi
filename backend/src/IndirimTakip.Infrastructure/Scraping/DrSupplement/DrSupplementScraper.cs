using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.DrSupplement;

/// <summary>
/// drsupplement.com.tr — İkas public storefront GraphQL kataloğu. Kaynak ürün
/// kategorilerini, gerçek üretici markasını, stokları ve vitrin fiyatını
/// yapısal olarak sağlıyor.
///
/// Aksesuar kategorisi elenir; takviyeler, sporcu gıdaları ve paketler korunur.
/// Herbina ürünlerinde üretici ile Dr Supplement satıcısı ayrı kaydedilir.
/// </summary>
public partial class DrSupplementScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Dr Supplement";
    public string BaseUrl => "https://drsupplement.com.tr";

    private const string MerchantId = "5acc350f-22b2-45be-901f-346289a7a1df";
    private const string SalesChannelId = "68402137-daa7-4000-8dfa-42a9b28fdd71";
    private const string StorefrontId = "5c3c8512-a554-4939-a4e2-f522e02ccc49";
    private const string SellerName = "drsupplement.com.tr";
    private const int PageSize = 100;

    // Sitenin kendi frontend'inin kullandığı public storefront JWT'si; yalnız
    // yukarıdaki merchant/storefront/salesChannel kimliklerini içeriyor.
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJtIjoiNWFjYzM1MGYtMjJiMi00NWJlLTkwMWYtMzQ2Mjg5YTdhMWRmIiwic2YiOiI1YzNjODUxMi1h" +
        "NTU0LTQ5MzktYTRlMi1mNTIyZTAyY2NjNDkiLCJzZnQiOjEsInNsIjoiNjg0MDIxMzctZGFhNy00MDAw" +
        "LThkZmEtNDJhOWIyOGZkZDcxIn0.Ro_S6b1lj_pRra6dHNgoD8Ji9hIM1HTS_5sFfF1uH6A";

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

    internal ScrapedProduct? Convert(DrSupplementProduct product)
    {
        var slug = product.MetaData?.Slug?.Trim('/');
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(product.Name))
            return null;

        var categories = product.Categories ?? [];
        if (categories.Any(category => category.Name?.Equals("AKSESUAR", StringComparison.OrdinalIgnoreCase) == true))
            return null;

        var rawBrand = product.Brand?.Name;
        if (string.IsNullOrWhiteSpace(rawBrand))
            return null;

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
        var (brandName, seller) = ResolveBrand(rawBrand);

        return new ScrapedProduct(
            Name: product.Name.Trim(),
            Url: $"{BaseUrl}/{slug}",
            ImageUrl: image is null
                ? null
                : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
            Category: ResolveCategory(product.Name, categories),
            Price: currentPrice,
            StoreOldPrice: currentPrice < sourcePrice.SellPrice ? sourcePrice.SellPrice : null,
            ServingsPerPackage: ExtractServingsPerPackage(product.Name),
            BrandName: brandName,
            InStock: product.Variants.Any(candidate => candidate.Stocks.Sum(stock => stock.StockCount) > 0),
            Seller: seller);
    }

    private static (string? BrandName, string? Seller) ResolveBrand(string rawBrand)
    {
        if (rawBrand.Equals("DR SUPPLEMENT", StringComparison.OrdinalIgnoreCase))
            return (null, null);

        if (rawBrand.Equals("HERBINA", StringComparison.OrdinalIgnoreCase))
            return ("Herbina", SellerName);

        var normalized = BrandNameNormalizer.Normalize(rawBrand);
        return string.IsNullOrWhiteSpace(normalized) ? (null, null) : (normalized, SellerName);
    }

    private static string? ResolveCategory(
        string productName,
        IReadOnlyCollection<DrSupplementCategory> categories)
    {
        var inferred = ProductAttributeParser.InferCategory(productName, "Dr Supplement");
        if (inferred is not null)
            return inferred;

        if (HasCategory(categories, "PROTEİN"))
            return "protein-tozu";
        if (HasCategory(categories, "CREATİN"))
            return "kreatin";
        if (HasCategory(categories, "PRE WORKOUT"))
            return "pre-workout";
        if (HasCategory(categories, "L-CARNİTİNE"))
            return "l-carnitine-cla";
        if (HasCategory(categories, "AMİNO ASİT") || HasCategory(categories, "BCAA"))
            return "amino-asitler";
        if (HasCategory(categories, "KİLO ALMA")
            || HasCategory(categories, "ÖĞÜN TOZU")
            || HasCategory(categories, "Meal")
            || HasCategory(categories, "PİRİNÇ UNLARI"))
        {
            return "kilo-hacim";
        }
        if (HasCategory(categories, "VİTAMİN & MİNERAL"))
            return "vitamin";
        if (HasCategory(categories, "ZAYIFLAMA"))
            return "yag-yakici";

        return null;
    }

    private static bool HasCategory(
        IReadOnlyCollection<DrSupplementCategory> categories,
        string expected) => categories.Any(category => category.Name?.Equals(expected, StringComparison.OrdinalIgnoreCase) == true);

    private static int? ExtractServingsPerPackage(string productName)
    {
        var match = ServingCountRegex().Match(productName);
        return match.Success
            && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value > 0
                ? value
                : null;
    }

    private async Task<DrSupplementGraphQlResponse?> SearchProductsPageAsync(
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
        return await response.Content.ReadFromJsonAsync<DrSupplementGraphQlResponse>(cancellationToken: cancellationToken);
    }

    [GeneratedRegex(@"\b(?<value>\d+)\s*SERV[İI]S\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServingCountRegex();
}

internal sealed class DrSupplementGraphQlResponse
{
    [JsonPropertyName("data")]
    public DrSupplementSearchData? Data { get; set; }
}

internal sealed class DrSupplementSearchData
{
    [JsonPropertyName("searchProducts")]
    public DrSupplementSearchResult? SearchProducts { get; set; }
}

internal sealed class DrSupplementSearchResult
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("results")]
    public List<DrSupplementProduct> Results { get; set; } = [];
}

internal sealed class DrSupplementProduct
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("brand")]
    public DrSupplementBrand? Brand { get; set; }

    [JsonPropertyName("metaData")]
    public DrSupplementMetaData? MetaData { get; set; }

    [JsonPropertyName("categories")]
    public List<DrSupplementCategory>? Categories { get; set; }

    [JsonPropertyName("variants")]
    public List<DrSupplementVariant> Variants { get; set; } = [];
}

internal sealed class DrSupplementBrand
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class DrSupplementMetaData
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}

internal sealed class DrSupplementCategory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class DrSupplementVariant
{
    [JsonPropertyName("prices")]
    public List<DrSupplementPrice> Prices { get; set; } = [];

    [JsonPropertyName("images")]
    public List<DrSupplementImage> Images { get; set; } = [];

    [JsonPropertyName("stocks")]
    public List<DrSupplementStock> Stocks { get; set; } = [];
}

internal sealed class DrSupplementPrice
{
    [JsonPropertyName("sellPrice")]
    public decimal SellPrice { get; set; }

    [JsonPropertyName("discountPrice")]
    public decimal? DiscountPrice { get; set; }
}

internal sealed class DrSupplementImage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }

    [JsonPropertyName("isMain")]
    public bool IsMain { get; set; }
}

internal sealed class DrSupplementStock
{
    [JsonPropertyName("stockCount")]
    public int StockCount { get; set; }
}

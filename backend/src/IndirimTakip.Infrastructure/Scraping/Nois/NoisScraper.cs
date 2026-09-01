using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Nois;

/// <summary>
/// nois.com — İkas public storefront GraphQL kataloğu. Kaynak kendi kategori
/// bilgisini, stoklarını ve vitrinde kullanılan indirimli fiyatı sağlıyor.
///
/// Kullanıcı kapsamı gereği yalnız aksesuarlar elenir. Sporcu gıdaları ve çoklu
/// ürün paketleri katalogda kalır. Katalogdaki tek üçüncü taraf ürünün kaynak
/// markası korunur ve Nois satıcı olarak işaretlenir.
/// </summary>
public partial class NoisScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Nois Nutrition";
    public string BaseUrl => "https://nois.com";

    private const string MerchantId = "07519355-6601-455f-a6f4-ed9ac5e06917";
    private const string SalesChannelId = "a2c85900-e094-4b1b-9fcc-76bd7517ad7d";
    private const string StorefrontId = "136b0556-c48b-4ac7-92a5-2dd278590157";
    private const string SellerName = "nois.com";
    private const int PageSize = 100;

    // Sitenin kendi frontend'inin gönderdiği public storefront JWT'si; yalnız
    // yukarıdaki merchant/storefront/salesChannel kimliklerini içeriyor.
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJtIjoiMDc1MTkzNTUtNjYwMS00NTVmLWE2ZjQtZWQ5YWM1ZTA2OTE3Iiwic2YiOiIxMzZiMDU1Ni1j" +
        "NDhiLTRhYzctOTJhNS0yZGQyNzg1OTAxNTciLCJzZnQiOjEsInNsIjoiYTJjODU5MDAtZTA5NC00YjFi" +
        "LTlmY2MtNzZiZDc1MTdhZDdkIn0.XVj0-8hNqq9M3FwU8mCDNURe2MruZsdpizZol2b0dBc";

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

    internal ScrapedProduct? Convert(NoisProduct product)
    {
        var slug = product.MetaData?.Slug?.Trim('/');
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(product.Name))
            return null;

        var categories = product.Categories ?? [];
        // Nois kategori alanı güvenilir ve aksesuarların üçü de açıkça
        // "Aksesuar" kategorisinde. Ortak isim filtresi burada kullanılmıyor:
        // "Cantaloupe" aroması "canta..." kalıbına yanlış pozitif veriyor.
        if (categories.Any(category => string.Equals(category.Name, "Aksesuar", StringComparison.OrdinalIgnoreCase)))
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

        var rawBrand = product.Brand?.Name;
        if (string.IsNullOrWhiteSpace(rawBrand))
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
        if (rawBrand.Equals("NOIS NUTRITION", StringComparison.OrdinalIgnoreCase)
            || rawBrand.Equals("NOIS", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        if (rawBrand.Equals("ORGANIKSATINAL", StringComparison.OrdinalIgnoreCase)
            || rawBrand.Equals("OrganikSatınAl", StringComparison.OrdinalIgnoreCase))
        {
            return ("OrganikSatınAl", SellerName);
        }

        var normalized = BrandNameNormalizer.Normalize(rawBrand);
        return string.IsNullOrWhiteSpace(normalized) ? (null, null) : (normalized, SellerName);
    }

    private static string? ResolveCategory(string productName, IReadOnlyCollection<NoisCategory> categories)
    {
        if (categories.Any(category => category.Name is not null
                && (category.Name.Equals("Gıda", StringComparison.OrdinalIgnoreCase)
                    || category.Name.Equals("PİRİNÇ PATLAKLARI", StringComparison.OrdinalIgnoreCase))))
        {
            return "saglikli-atistirmaliklar";
        }

        if (categories.Any(category => category.Name?.Equals("Kilo ve Hacim", StringComparison.OrdinalIgnoreCase) == true))
            return "kilo-hacim";

        if (categories.Any(category => category.Name?.Equals("Protein", StringComparison.OrdinalIgnoreCase) == true))
            return "protein-tozu";

        var inferred = ProductAttributeParser.InferCategory(productName, "Nois Nutrition");
        if (inferred is not null)
            return inferred;

        if (categories.Any(category => category.Name?.Equals("Amino Asit", StringComparison.OrdinalIgnoreCase) == true))
            return "amino-asitler";

        if (categories.Any(category => category.Name?.Equals("Yağ Yakım", StringComparison.OrdinalIgnoreCase) == true))
            return "yag-yakici";

        return null;
    }

    private static int? ExtractServingsPerPackage(string productName)
    {
        var match = ServingCountRegex().Match(productName);
        return match.Success
            && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value > 0
                ? value
                : null;
    }

    private async Task<NoisGraphQlResponse?> SearchProductsPageAsync(int page, CancellationToken cancellationToken)
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
        return await response.Content.ReadFromJsonAsync<NoisGraphQlResponse>(cancellationToken: cancellationToken);
    }

    [GeneratedRegex(@"\b(?<value>\d+)\s*Servis\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServingCountRegex();
}

internal sealed class NoisGraphQlResponse
{
    [JsonPropertyName("data")]
    public NoisSearchData? Data { get; set; }
}

internal sealed class NoisSearchData
{
    [JsonPropertyName("searchProducts")]
    public NoisSearchResult? SearchProducts { get; set; }
}

internal sealed class NoisSearchResult
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("results")]
    public List<NoisProduct> Results { get; set; } = [];
}

internal sealed class NoisProduct
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("brand")]
    public NoisBrand? Brand { get; set; }

    [JsonPropertyName("metaData")]
    public NoisMetaData? MetaData { get; set; }

    [JsonPropertyName("categories")]
    public List<NoisCategory>? Categories { get; set; }

    [JsonPropertyName("variants")]
    public List<NoisVariant> Variants { get; set; } = [];
}

internal sealed class NoisBrand
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class NoisMetaData
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}

internal sealed class NoisCategory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class NoisVariant
{
    [JsonPropertyName("prices")]
    public List<NoisPrice> Prices { get; set; } = [];

    [JsonPropertyName("images")]
    public List<NoisImage> Images { get; set; } = [];

    [JsonPropertyName("stocks")]
    public List<NoisStock> Stocks { get; set; } = [];
}

internal sealed class NoisPrice
{
    [JsonPropertyName("sellPrice")]
    public decimal SellPrice { get; set; }

    [JsonPropertyName("discountPrice")]
    public decimal? DiscountPrice { get; set; }
}

internal sealed class NoisImage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }

    [JsonPropertyName("isMain")]
    public bool IsMain { get; set; }
}

internal sealed class NoisStock
{
    [JsonPropertyName("stockCount")]
    public int StockCount { get; set; }
}

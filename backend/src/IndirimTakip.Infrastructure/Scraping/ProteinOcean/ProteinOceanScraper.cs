using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.ProteinOcean;

// İkas'ın public storefront GraphQL API'sini (api.myikas.com/api/sf/graphql)
// doğrudan çağırıyoruz — sitenin kendi frontend'i de tarayıcıda bunu kullanıyor,
// Playwright/JS render gerektirmiyor. İstek şekli, sayfa gerçek tarayıcıda
// açılıp "Daha fazla göster" tıklanarak ağ trafiği izlenerek keşfedildi.
public partial class ProteinOceanScraper(HttpClient httpClient) : IBrandScraper, IProductDescriptionFetcher
{
    public string BrandName => "ProteinOcean";
    public string BaseUrl => "https://proteinocean.com";

    private const string MerchantId = "00b6c111-71dc-4400-932f-8db87e5da64c";
    private const string SalesChannelId = "23808606-a836-4b48-8bed-3d16a0285e15";

    // "Tüm Ürünler" (proteinocean.com/tum-urunler) kategorisinin İkas kategori ID'si.
    // Tek bu kategoriyi sorgulamak, tüm alt kategorileri (protein/vitamin/vb.) tek
    // tek gezmek yerine bütün kataloğu tek sorgu tipiyle almamızı sağlıyor.
    private const string AllProductsCategoryId = "2f1ed40a-e061-4d4b-81f7-09684bcdf766";

    private const int PageSize = 100; // İkas sunucusu perPage'i 100 ile sınırlıyor.

    // Sitenin kendi frontend'inin kullandığı public storefront API anahtarı — JWT
    // içinde sadece merchant/storefront/salesChannel ID'leri var, gizli bir kimlik
    // bilgisi değil, her ziyaretçinin taretcisi zaten bunu gönderiyor.
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtIjoiMDBiNmMxMTEtNzFkYy00NDAwLTkzMmYtOGRiODdlNWRhNjRjIiwic2YiOiIwZWFkMTAwNS1iMTljLTQ2ODAtOGFiNy1hOGExY2NhMzI0NGUiLCJzZnQiOjEsInNsIjoiMjM4MDg2MDYtYTgzNi00YjQ4LThiZWQtM2QxNmEwMjg1ZTE1In0.pCx2DN2rV_D42D3B4b68r4pVliGdMm3LR0Gos13GdHU";

    private const string StorefrontId = "0ead1005-b19c-4680-8ab7-a8a1cca3244e";

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

                var variant = product.Variants.Find(v => v.Stocks.Sum(s => s.StockCount) > 0)
                    ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Prices.Count == 0)
                    continue;

                var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

                products.Add(new ScrapedProduct(
                    Name: product.Name,
                    Url: $"{BaseUrl}/{product.MetaData.Slug}",
                    ImageUrl: image is null
                        ? null
                        : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
                    Category: null,
                    Price: variant.Prices[0].SellPrice));
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
                    categoryIdList = new[] { AllProductsCategoryId },
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

    // Ürün açıklaması GraphQL searchProducts'ta yok (description alanı sadece
    // 24-89 karakterlik bir slogan) — gerçek/uzun açıklama sayfanın kendi
    // sunucu-render HTML'inde (__NEXT_DATA__ script'i) geliyor. GraphQL API
    // BaseAddress'i api.myikas.com olsa da, HttpClient'a MUTLAK bir URL
    // verildiğinde BaseAddress göz ardı edilir — o yüzden aynı HttpClient
    // doğrudan proteinocean.com'a istek atabiliyor.
    public async Task<string?> FetchDescriptionAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, productUrl);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractDescription(html);
    }

    // __NEXT_DATA__ içindeki props.pageProps.pageSpecificData.attributes dizisi
    // — her eleman productAttribute.type ile HTML/TABLE tipini belirtiyor.
    // Sadece HTML olanları (Açıklama/Özellikler/Kullanım Şekli gibi) alıyoruz;
    // TABLE olanları (amino asit/besin tabloları) düz metne dönüştürülmeye
    // uygun değil, atlanıyor.
    private static string? ExtractDescription(string html)
    {
        var match = NextDataRegex().Match(html);
        if (!match.Success)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            if (!doc.RootElement.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var pageProps) ||
                !pageProps.TryGetProperty("pageSpecificData", out var pageSpecificData) ||
                !pageSpecificData.TryGetProperty("attributes", out var attributes) ||
                attributes.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var sections = new List<string>();
            foreach (var attribute in attributes.EnumerateArray())
            {
                if (!attribute.TryGetProperty("productAttribute", out var productAttribute) ||
                    !productAttribute.TryGetProperty("type", out var typeProp) ||
                    typeProp.GetString() != "HTML")
                {
                    continue;
                }

                var name = productAttribute.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var value = attribute.TryGetProperty("value", out var valueProp) ? valueProp.GetString() : null;
                var text = StripHtml(value);
                if (text.Length == 0)
                    continue;

                var title = CleanTitle(name);
                sections.Add(string.IsNullOrEmpty(title) ? text : $"{title}: {text}");
            }

            return sections.Count == 0 ? null : string.Join("\n\n", sections);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // "2- ÜRÜN SAYFA - ÖZELLİKLER" gibi dahili/numaralı başlıkları "ÖZELLİKLER"e sadeleştiriyor.
    private static string CleanTitle(string? name) =>
        string.IsNullOrEmpty(name) ? "" : TitlePrefixRegex().Replace(name, "").Trim();

    private static string StripHtml(string? html) =>
        string.IsNullOrEmpty(html)
            ? ""
            : System.Net.WebUtility.HtmlDecode(TagRegex().Replace(html, " ")).Trim().Replace("  ", " ");

    [GeneratedRegex(@"<script id=""__NEXT_DATA__""[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();

    [GeneratedRegex(@"^\d+-\s*ÜRÜN SAYFA\s*-\s*", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePrefixRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
}

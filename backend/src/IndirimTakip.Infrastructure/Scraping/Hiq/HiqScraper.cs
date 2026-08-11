using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Hiq;

public partial class HiqScraper(HttpClient httpClient) : IBrandScraper
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
                    Price: variant.Price,
                    ServingSizeGrams: ExtractServingSizeGrams(product.BodyHtml)));
            }

            if (response.Products.Count < 250)
                break;
        }

        return result;
    }

    // HIQ'nun ürün açıklamasında (body_html) gerçek bir besin değeri tablosu
    // geliyor: <table class="nutrition-table"><tr><th>Bileşen</th><th>100 g</th>
    // <th>4 g</th></tr>...</table> — son başlık hücresi porsiyon büyüklüğü.
    // Tabloda gram cinsinden değilse (kapsül/tablet ürünler gibi) null döneriz;
    // uydurmak yerine boş bırakmayı tercih ediyoruz.
    private static decimal? ExtractServingSizeGrams(string? bodyHtml)
    {
        if (string.IsNullOrEmpty(bodyHtml))
            return null;

        var tableIndex = bodyHtml.IndexOf("nutrition-table", StringComparison.Ordinal);
        if (tableIndex < 0)
            return null;

        var headerMatch = HeaderRowRegex().Match(bodyHtml[tableIndex..]);
        if (!headerMatch.Success)
            return null;

        var headerCells = HeaderCellRegex().Matches(headerMatch.Value)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        if (headerCells.Count < 2)
            return null;

        var gramMatch = ServingGramsRegex().Match(headerCells[^1]);
        if (!gramMatch.Success)
            return null;

        return decimal.Parse(gramMatch.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"<tr>\s*(<th>.*?</th>\s*){2,}</tr>", RegexOptions.Singleline)]
    private static partial Regex HeaderRowRegex();

    [GeneratedRegex(@"<th>(.*?)</th>")]
    private static partial Regex HeaderCellRegex();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*g$", RegexOptions.IgnoreCase)]
    private static partial Regex ServingGramsRegex();
}

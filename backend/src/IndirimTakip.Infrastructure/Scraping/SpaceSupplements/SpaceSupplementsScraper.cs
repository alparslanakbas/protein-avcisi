using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.SpaceSupplements;

/// <summary>
/// spacegymsupplements.com — kendi sitesinden satan, tek markalı özel
/// Laravel/PHP mağazası. Public sitemap ürün adreslerini; ürün sayfasındaki
/// schema.org Product bloğu gerçek ad, fiyat, görsel, açıklama ve stok
/// durumunu sağlıyor.
///
/// Sitemap'teki shaker ve çanta takviye olmadığı için ortak filtreyle
/// dışarıda bırakılır. Kategori, markalı ürün adları tek başına yeterli
/// olmadığında schema.org açıklamasındaki açık bileşen/type ifadesinden
/// çıkarılır; paket gramajı SKU'dan tahmin edilmez.
/// </summary>
public partial class SpaceSupplementsScraper(
    HttpClient httpClient,
    ILogger<SpaceSupplementsScraper> logger) : IBrandScraper
{
    public string BrandName => "Space";
    public string BaseUrl => "https://spacegymsupplements.com";

    private const string SitemapPath = "sitemap.xml";
    private const double MaxFailureRatio = 0.2;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchProductUrlsAsync(cancellationToken);
        if (urls.Count == 0)
            throw new InvalidOperationException("Space ürün sitemap'i hiç adres döndürmedi.");

        var products = new List<ScrapedProduct>();
        var skipped = 0;
        var missing = 0;
        var failures = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    missing++;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new SpaceRateLimitException();
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    var product = ParseProduct(html, url);
                    if (product is null)
                        skipped++;
                    else
                        products.Add(product);
                }
            }
            catch (SpaceRateLimitException)
            {
                throw;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        if (failures > urls.Count * MaxFailureRatio)
        {
            throw new InvalidOperationException(
                $"Space: {urls.Count} adresin {failures} tanesinde beklenmeyen hata oluştu, tarama güvenilir değil.");
        }

        logger.LogInformation(
            "Space: {Total} adres tarandı, {Found} ürün alındı, {Skipped} kapsam dışı/geçersiz ürün, " +
            "{Missing} bulunamayan adres, {Failures} hata.",
            urls.Count, products.Count, skipped, missing, failures);

        return products;
    }

    private async Task<List<string>> FetchProductUrlsAsync(CancellationToken cancellationToken)
    {
        var xml = await httpClient.GetStringAsync(SitemapPath, cancellationToken);
        return ParseSitemapUrls(xml);
    }

    internal static List<string> ParseSitemapUrls(string xml)
    {
        var document = XDocument.Parse(xml);
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

        return document
            .Descendants(sitemap + "loc")
            .Select(node => node.Value.Trim())
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && uri.Host.Equals("spacegymsupplements.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith("/urunler/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static ScrapedProduct? ParseProduct(string? html, string url)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var scripts = document.DocumentNode.SelectNodes(
            "//script[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='application/ld+json']");
        if (scripts is null)
            return null;

        foreach (var script in scripts)
        {
            var json = WebUtility.HtmlDecode(script.InnerText).Trim();
            if (json.Length == 0)
                continue;

            try
            {
                using var parsed = JsonDocument.Parse(json);
                if (TryParseProductElement(parsed.RootElement, html, url, out var product))
                    return product;
            }
            catch (JsonException)
            {
                // Product dışı bozuk bir JSON-LD bloğu diğer geçerli bloğun
                // okunmasını engellememeli.
            }
        }

        return null;
    }

    private static bool TryParseProductElement(
        JsonElement element,
        string html,
        string url,
        out ScrapedProduct? product)
    {
        product = null;

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (TryParseProductElement(child, html, url, out product))
                    return true;
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!IsProduct(element))
        {
            if (element.TryGetProperty("@graph", out var graph))
                return TryParseProductElement(graph, html, url, out product);

            return false;
        }

        var name = ReadString(element, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return true;

        var brand = ReadBrand(element);
        if (!string.Equals(brand, "Space", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!element.TryGetProperty("offers", out var offers)
            || !TryReadOffer(offers, out var price, out var inStock)
            || price <= 0)
        {
            return true;
        }

        var description = ReadString(element, "description")?.Trim();
        var categoryText = description is null ? name : $"{name} {description}";

        product = new ScrapedProduct(
            Name: name,
            Url: url,
            ImageUrl: ReadImage(element),
            Category: ProductAttributeParser.InferCategory(categoryText, "Space"),
            Price: price,
            ServingSizeGrams: ReadServingSizeGrams(html),
            Description: description,
            InStock: inStock);

        return true;
    }

    private static bool IsProduct(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
            return false;

        if (type.ValueKind == JsonValueKind.String)
            return type.GetString()?.Equals("Product", StringComparison.OrdinalIgnoreCase) == true;

        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String
                && value.GetString()?.Equals("Product", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? ReadBrand(JsonElement product)
    {
        if (!product.TryGetProperty("brand", out var brand))
            return null;

        var value = brand.ValueKind switch
        {
            JsonValueKind.String => brand.GetString(),
            JsonValueKind.Object => ReadString(brand, "name"),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryReadOffer(JsonElement offers, out decimal price, out bool? inStock)
    {
        price = 0;
        inStock = null;

        var offer = offers.ValueKind == JsonValueKind.Array
            ? offers.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object)
            : offers;

        if (offer.ValueKind != JsonValueKind.Object || !offer.TryGetProperty("price", out var priceElement))
            return false;

        if (priceElement.ValueKind == JsonValueKind.Number)
        {
            if (!priceElement.TryGetDecimal(out price))
                return false;
        }
        else if (priceElement.ValueKind != JsonValueKind.String
            || !decimal.TryParse(priceElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out price))
        {
            return false;
        }

        var availability = ReadString(offer, "availability");
        if (availability?.Contains("OutOfStock", StringComparison.OrdinalIgnoreCase) == true)
            inStock = false;
        else if (availability?.Contains("InStock", StringComparison.OrdinalIgnoreCase) == true)
            inStock = true;

        return true;
    }

    private static string? ReadImage(JsonElement product)
    {
        if (!product.TryGetProperty("image", out var image))
            return null;

        if (image.ValueKind == JsonValueKind.Array)
            image = image.EnumerateArray().FirstOrDefault();

        return image.ValueKind switch
        {
            JsonValueKind.String => image.GetString(),
            JsonValueKind.Object => ReadString(image, "contentUrl") ?? ReadString(image, "url"),
            _ => null,
        };
    }

    private static decimal? ReadServingSizeGrams(string html)
    {
        var match = ServingSizeRegex().Match(WebUtility.HtmlDecode(html));
        if (!match.Success)
            return null;

        return decimal.TryParse(
            match.Groups["value"].Value.Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex(
        @"SERVİS:\s*1\s*Ölçek\s*\(\s*(?<value>\d+(?:[.,]\d+)?)\s*(?:G|GR)\s*\)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ServingSizeRegex();

    private sealed class SpaceRateLimitException()
        : InvalidOperationException("Space hız sınırı (429) uyguladı; kaynak zorlanmamak için tarama durduruldu.");
}

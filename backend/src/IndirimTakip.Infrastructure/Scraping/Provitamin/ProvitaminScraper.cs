using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Provitamin;

/// <summary>
/// provitamin.com.tr — Wix tabanlı çok markalı bir BAYİ. Wix mağaza API'si
/// anonim sunucu isteklerini 403 ile reddediyor; public ürün sitemap'i ise
/// bütün ürün adreslerini, ürün sayfalarındaki schema.org Product bloğu da
/// gerçek üretici, fiyat, görsel ve stok durumunu sağlıyor.
///
/// Ürün başına istek gerektiği için <see cref="DailyOnly"/>: 430 ürünü altı
/// saatte bir çekmek hem gereksiz yük hem engellenme riski olurdu. Ürünler
/// gerçek üreticisi altında, satıcı <c>provitamin.com.tr</c> olarak kaydedilir.
/// </summary>
public class ProvitaminScraper(HttpClient httpClient, ILogger<ProvitaminScraper> logger) : IBrandScraper
{
    public string BrandName => "Provitamin";
    public string BaseUrl => "https://www.provitamin.com.tr";
    public bool DailyOnly => true;

    private const string ProductSitemapPath = "store-products-sitemap.xml";
    private const string SellerName = "provitamin.com.tr";
    private const double MaxFailureRatio = 0.2;

    // Wix, yaklaşık bir saniye aralıklı uzun seride 429 uyguluyor (canlı tam
    // katalog testinde 54 başarılı istekten sonra ölçüldü). Üç saniye aralık
    // tam taramayı yaklaşık 22 dakikada bitirir; günde bir çalışan kaynak için
    // kabul edilebilir ve karşı sitenin hız sınırına saygılıdır.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(3);

    private static readonly IReadOnlyDictionary<string, string> BrandAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Big Joy"] = "BigJoy",
            ["Protein Ocean"] = "ProteinOcean",
            ["Proteinocean"] = "ProteinOcean",
            ["Swiss"] = "Swiss Nutrition",
            ["Trec Nutrition"] = "Trec",
            ["Universal Nutrition"] = "Universal",
            ["Zero Shot"] = "ZeroShot",
        };

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchProductUrlsAsync(cancellationToken);
        if (urls.Count == 0)
            throw new InvalidOperationException("Provitamin ürün sitemap'i hiç adres döndürmedi.");

        var products = new List<ScrapedProduct>();
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
                    // Aynı turda tekrar deneyip hız sınırını uzatma. Yutma
                    // servisi mevcut veriyi silmiyor; bir sonraki günlük tur
                    // temiz bir pencerede yeniden deneyecek.
                    throw new ProvitaminRateLimitException();
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    var product = ParseProduct(html, url);
                    if (product is not null)
                        products.Add(product);
                }
            }
            catch (ProvitaminRateLimitException)
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
                $"Provitamin: {urls.Count} adresin {failures} tanesinde beklenmeyen hata oluştu, tarama güvenilir değil.");
        }

        logger.LogInformation(
            "Provitamin: {Total} adres tarandı, {Found} ürün alındı, {Missing} adres bulunamadı, {Failures} hata.",
            urls.Count, products.Count, missing, failures);

        return products;
    }

    private async Task<List<string>> FetchProductUrlsAsync(CancellationToken cancellationToken)
    {
        var xml = await httpClient.GetStringAsync(ProductSitemapPath, cancellationToken);
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
                && uri.Host.Equals("www.provitamin.com.tr", StringComparison.OrdinalIgnoreCase))
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
            var json = HtmlEntity.DeEntitize(script.InnerText).Trim();
            if (json.Length == 0)
                continue;

            try
            {
                using var parsed = JsonDocument.Parse(json);
                if (TryParseProductElement(parsed.RootElement, url, out var product))
                    return product;
            }
            catch (JsonException)
            {
                // Sayfada Product dışı bozuk bir JSON-LD bloğu varsa diğer
                // bloklara bakmaya devam et; hiçbir geçerli ürün yoksa null.
            }
        }

        return null;
    }

    private static bool TryParseProductElement(JsonElement element, string url, out ScrapedProduct? product)
    {
        product = null;

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (TryParseProductElement(child, url, out product))
                    return true;
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!IsProduct(element))
        {
            if (element.TryGetProperty("@graph", out var graph))
                return TryParseProductElement(graph, url, out product);

            return false;
        }

        var name = ReadString(element, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return true;

        var brand = ReadBrand(element);
        if (brand is null)
            return true;

        if (!element.TryGetProperty("offers", out var offers) || !TryReadOffer(offers, out var price, out var inStock))
            return true;

        if (price <= 0)
            return true;

        product = new ScrapedProduct(
            Name: name,
            Url: url,
            ImageUrl: ReadImage(element),
            Category: null,
            Price: price,
            BrandName: NormalizeBrand(brand),
            InStock: inStock,
            Seller: SellerName);

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

    internal static string NormalizeBrand(string brand) =>
        BrandAliases.TryGetValue(brand.Trim(), out var canonical) ? canonical : brand.Trim();

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

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed class ProvitaminRateLimitException()
        : InvalidOperationException("Provitamin hız sınırı (429) uyguladı; tarama kaynağı zorlamamak için durduruldu.");
}

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.MusclePump;

/// <summary>
/// musclepump.com.tr — AKINSOFT tabanlı çok markalı mağaza. Public ürün
/// sitemap'i adresleri, ürün sayfasındaki ana detay bloğu ise gerçek ürün adı,
/// üretici, fiyat, mağaza eski fiyatı, görsel ve satın alınabilirlik durumunu
/// sağlıyor.
///
/// Fitness aksesuarları ile kombinasyon altında listelenen satış standları,
/// sitemap yolundan istek atılmadan elenir. Muscle Pump dışındaki üreticiler
/// (canlı katalogda Sygenix) gerçek marka adıyla ve musclepump.com.tr
/// satıcısıyla ayrı kaydedilir.
/// </summary>
public partial class MusclePumpScraper(
    HttpClient httpClient,
    ILogger<MusclePumpScraper> logger) : IBrandScraper
{
    public string BrandName => "Muscle Pump";
    public string BaseUrl => "https://musclepump.com.tr";

    private const string ProductSitemapPath = "products_1.xml";
    private const string SellerName = "musclepump.com.tr";
    private const double MaxFailureRatio = 0.2;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchProductUrlsAsync(cancellationToken);
        if (urls.Count == 0)
            throw new InvalidOperationException("Muscle Pump ürün sitemap'i hiç takviye adresi döndürmedi.");

        var products = new List<ScrapedProduct>();
        var productKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        var missing = 0;
        var duplicates = 0;
        var failures = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    missing++;
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new MusclePumpRateLimitException();
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    var product = ParseProduct(html, url);
                    if (product is null)
                    {
                        skipped++;
                    }
                    else if (productKeys.Add($"{product.Url}\n{product.Name}"))
                    {
                        products.Add(product);
                    }
                    else
                    {
                        duplicates++;
                    }
                }
            }
            catch (MusclePumpRateLimitException)
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
                $"Muscle Pump: {urls.Count} adresin {failures} tanesinde beklenmeyen hata oluştu, tarama güvenilir değil.");
        }

        logger.LogInformation(
            "Muscle Pump: {Total} takviye adresi tarandı, {Found} ürün alındı, {Skipped} geçersiz ürün, " +
            "{Missing} bulunamayan adres, {Duplicates} yinelenen canonical ürün, {Failures} hata.",
            urls.Count, products.Count, skipped, missing, duplicates, failures);

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
            .Select(node => NormalizeProductUrl(node.Value))
            .Where(url => url is not null && !IsAccessoryPath(new Uri(url).AbsolutePath))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static ScrapedProduct? ParseProduct(string? html, string requestedUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var detailRegion = document.DocumentNode.SelectSingleNode(
            "//detail-region[@itemscope]");
        var detailRight = detailRegion?.SelectSingleNode(
            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' detailRightBlock ')]");
        if (detailRegion is null || detailRight is null)
            return null;

        var canonicalValue = document.DocumentNode.SelectSingleNode(
            "//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='canonical']")
            ?.GetAttributeValue("href", string.Empty);
        var productUrl = NormalizeProductUrl(WebUtility.HtmlDecode(canonicalValue ?? string.Empty))
            ?? NormalizeProductUrl(requestedUrl);
        if (productUrl is null || IsAccessoryPath(new Uri(productUrl).AbsolutePath))
        {
            return null;
        }

        var name = WebUtility.HtmlDecode(
                detailRight.SelectSingleNode(".//strong[@itemprop='name']")?.InnerText ?? string.Empty)
            .Trim();
        if (name.Length == 0 || NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return null;

        var priceArea = detailRight.SelectSingleNode(
            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' detailPriceBlock ')]");
        var priceValue = priceArea?.SelectSingleNode(".//meta[@itemprop='price']")
            ?.GetAttributeValue("content", string.Empty);
        if (!decimal.TryParse(priceValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || price <= 0m)
        {
            return null;
        }

        var oldPriceText = WebUtility.HtmlDecode(
            priceArea?.SelectSingleNode(".//strike")?.InnerText ?? string.Empty).Trim();
        var oldPrice = TryParseTurkishPrice(oldPriceText);
        if (oldPrice is null or <= 0m || oldPrice <= price)
            oldPrice = null;

        var rawBrand = WebUtility.HtmlDecode(
                detailRight.SelectSingleNode(".//a[@itemprop='brand']")?.InnerText ?? string.Empty)
            .Trim();
        if (rawBrand.Length == 0)
            return null;

        var (brandName, seller) = ResolveBrand(rawBrand);
        var productText = WebUtility.HtmlDecode(
            detailRegion.SelectSingleNode(".//*[@id='nav-description']")?.InnerText ?? string.Empty);
        var imageValue = document.DocumentNode.SelectSingleNode("//meta[@property='og:image']")
            ?.GetAttributeValue("content", string.Empty);
        var imageUrl = NormalizeImageUrl(WebUtility.HtmlDecode(imageValue ?? string.Empty));

        return new ScrapedProduct(
            Name: name,
            Url: productUrl,
            ImageUrl: imageUrl,
            Category: ResolveCategory(name, productUrl),
            Price: price,
            // Kaynak başlığı büyük noktalı İ ile "PORSİYON" yazıyor;
            // invariant regex eşleşmesi için noktalı/noktasız I sadeleştirilir.
            ServingSizeGrams: ProductAttributeParser.ExtractServingSizeGrams(
                productText.Replace('İ', 'i').Replace('I', 'i')),
            StoreOldPrice: oldPrice,
            ServingsPerPackage: ExtractServingsPerPackage(productText),
            BrandName: brandName,
            InStock: HasEnabledBasketButton(detailRight),
            Seller: seller);
    }

    private static bool HasEnabledBasketButton(HtmlNode detailRight) =>
        detailRight.SelectSingleNode(
            ".//button[@data-basket-add='true' and not(@disabled)]") is not null;

    private static bool IsAccessoryPath(string path) =>
        path.StartsWith("/fitness-aksesuarlari/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/kombinasyon/stand/", StringComparison.OrdinalIgnoreCase);

    private static (string? BrandName, string? Seller) ResolveBrand(string rawBrand)
    {
        if (rawBrand.Equals("MUSCLE PUMP", StringComparison.OrdinalIgnoreCase))
            return (null, null);
        if (rawBrand.Equals("SYGENIX", StringComparison.OrdinalIgnoreCase))
            return ("Sygenix", SellerName);

        return (BrandNameNormalizer.Normalize(rawBrand), SellerName);
    }

    private static string? ResolveCategory(string name, string url)
    {
        var inferred = ProductAttributeParser.InferCategory(name, "Muscle Pump");
        if (inferred is not null)
            return inferred;

        // Kaynağın tek kullanımlık kategorisi ürün türünü taşımıyor; iki
        // gerçek üründe kullanılan Pre-Venom adı markanın pre-workout ürünüdür.
        if (name.Contains("Pre-Venom", StringComparison.OrdinalIgnoreCase))
            return "pre-workout";

        var path = new Uri(url).AbsolutePath;
        if (path.StartsWith("/protein-tozu/", StringComparison.OrdinalIgnoreCase))
            return "protein-tozu";
        if (path.StartsWith("/amino-asitler/", StringComparison.OrdinalIgnoreCase))
            return "amino-asitler";
        if (path.StartsWith("/l-karnitin-ve-cla/", StringComparison.OrdinalIgnoreCase))
            return "l-carnitine-cla";
        if (path.StartsWith("/kilo-ve-hacim/", StringComparison.OrdinalIgnoreCase))
            return "kilo-hacim";
        if (path.StartsWith("/vitaminler/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/performansguc/tribulus/", StringComparison.OrdinalIgnoreCase))
        {
            return "vitamin";
        }
        if (path.StartsWith("/performansguc/kreatin/", StringComparison.OrdinalIgnoreCase))
            return "kreatin";
        if (path.StartsWith("/performansguc/guc-ve-performans/", StringComparison.OrdinalIgnoreCase))
            return "pre-workout";

        return null;
    }

    private static int? ExtractServingsPerPackage(string text)
    {
        var match = ServingCountRegex().Match(text);
        return match.Success
            && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value > 0
                ? value
                : null;
    }

    private static decimal? TryParseTurkishPrice(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return TurkishPriceParser.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? NormalizeProductUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.AbsolutePath.Contains("/prd-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
        if (!host.Equals("musclepump.com.tr", StringComparison.OrdinalIgnoreCase))
            return null;

        return $"https://musclepump.com.tr{uri.AbsolutePath.TrimEnd('/')}";
    }

    private static string? NormalizeImageUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var absolute)
            && absolute.Scheme == Uri.UriSchemeHttps)
        {
            return absolute.ToString();
        }

        return Uri.TryCreate(new Uri("https://musclepump.com.tr/"), value.TrimStart('/'), out var relative)
            ? relative.ToString()
            : null;
    }

    [GeneratedRegex(@"SERV[İI]S\s*SAYISI\s*:\s*(?<value>\d+)\s*SERV[İI]S\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServingCountRegex();

    private sealed class MusclePumpRateLimitException()
        : InvalidOperationException("Muscle Pump hız sınırı (429) uyguladı; kaynak zorlanmamak için tarama durduruldu.");
}

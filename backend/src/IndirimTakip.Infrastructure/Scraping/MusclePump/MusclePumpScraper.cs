using System.Globalization;
using System.Net;
using System.Text.Json;
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
    ILogger<MusclePumpScraper> logger) : IBrandScraper, IProductDetailFetcher
{
    public string BrandName => "Muscle Pump";
    public string BaseUrl => "https://musclepump.com.tr";

    private const string ProductSitemapPath = "products_1.xml";
    private const string SellerName = "musclepump.com.tr";
    private const double MaxFailureRatio = 0.2;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(300);


    /// <summary>
    /// Besin değeri — yalnızca ürün DETAY sayfasındaki açıklama sekmesinde.
    /// </summary>
    /// <remarks>
    /// Kaynak klasik bir <c>&lt;table&gt;</c> kullanıyor ama iki tuzağı var,
    /// ikisi de <see cref="HtmlNutritionExtractor.FromMultiColumnTable"/>
    /// içinde ele alınıyor:
    /// <list type="number">
    /// <item>Sütunlar <c>BESİN | 100gr İÇİN | 100gr RA* % | 30gr İÇİN |
    /// 30gr RA* %</c> — SON sütun yüzde. Genel <c>FromTables</c> son sütunu
    /// aldığı için burada sessizce yanlış değer yazardı.</item>
    /// <item>Bazı satırlar iki besini tek hücreye <c>&lt;br&gt;</c> ile
    /// koyuyor (YAĞ / DOYMUŞ YAĞ).</item>
    /// </list>
    ///
    /// <b>Porsiyon büyüklüğü uydurulmuyor:</b> seçilen sütunun başlığında
    /// yazılı ("30gr İÇİN"), yani kaynağın kendi beyanı. Başlıkta gramaj
    /// yoksa alan boş kalıyor.
    ///
    /// Açıklama BİLEREK çekilmiyor — normal taramada zaten geliyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromMultiColumnTable(doc.DocumentNode));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: nutritionJson is null
                ? null
                : NutritionServingParser.Grams(HtmlNutritionExtractor.MultiColumnPortionHeader(doc.DocumentNode)),
            ServingsPerPackage: null);
    }

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

        // SITE MİKRO VERİYİ KALDIRDI (3 Eylül'de canlıda yakalandı).
        //
        // Ad, fiyat ve marka eskiden `itemprop` mikro verisinden okunuyordu
        // (`strong[@itemprop='name']`, `meta[@itemprop='price']`,
        // `a[@itemprop='brand']`). Site bunları TAMAMEN kaldırdı — sayfada
        // artık tek bir `itemscope` bile yok — ve yerine JSON-LD koydu.
        // Sonuç sessiz bir bozulmaydı: tarama hatasız görünüyor ama
        // "136 adres tarandı, 0 ürün alındı, 136 geçersiz ürün" diyordu.
        // Kullanıcı bir ürünün fiyat geçmişinin "dün"de kalmasından fark etti.
        //
        // Yeni kaynak JSON-LD ve `@graph` DİZİSİ kullanıyor — düz bir
        // Product nesnesi değil, tipli düğümlerin listesi; Product düğümü
        // onun içinden aranıyor.
        var urunDugumu = SchemaOrgProductNode(document);
        if (urunDugumu is null)
            return null;

        var detailRegion = document.DocumentNode.SelectSingleNode("//detail-region");
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

        var name = (urunDugumu.Value.TryGetProperty("name", out var adAlani)
            ? WebUtility.HtmlDecode(adAlani.GetString() ?? string.Empty)
            : string.Empty).Trim();
        if (name.Length == 0 || NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return null;

        var teklif = SchemaOrgOffer(urunDugumu.Value);
        var priceValue = teklif is not null && teklif.Value.TryGetProperty("price", out var fiyatAlani)
            ? (fiyatAlani.ValueKind == JsonValueKind.String ? fiyatAlani.GetString() : fiyatAlani.ToString())
            : null;
        // JSON-LD fiyatı NOKTA ondalıklı ("3589.76"); Türkçe kültürle
        // ayrıştırılsaydı 358976 çıkardı.
        if (!decimal.TryParse(priceValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || price <= 0m)
        {
            return null;
        }

        // Mağazanın beyan ettiği eski fiyat hâlâ DOM'da, <strike> içinde.
        // Sayfada on tane <strike> var (benzer ürünler dahil), o yüzden
        // kapsam fiyat kutusuyla sınırlı tutuluyor.
        var priceArea = detailRight.SelectSingleNode(
            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' detailPriceBlock ')]");

        var oldPriceText = WebUtility.HtmlDecode(
            priceArea?.SelectSingleNode(".//strike")?.InnerText ?? string.Empty).Trim();
        var oldPrice = TryParseTurkishPrice(oldPriceText);
        if (oldPrice is null or <= 0m || oldPrice <= price)
            oldPrice = null;

        var rawBrand = (urunDugumu.Value.TryGetProperty("brand", out var markaAlani)
            && markaAlani.ValueKind == JsonValueKind.Object
            && markaAlani.TryGetProperty("name", out var markaAdi)
                ? WebUtility.HtmlDecode(markaAdi.GetString() ?? string.Empty)
                : string.Empty).Trim();
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

    /// <summary>
    /// Sayfadaki JSON-LD bloklarından schema.org <c>Product</c> düğümünü
    /// bulur.
    ///
    /// Muscle Pump düz bir Product nesnesi DEĞİL, <c>@graph</c> dizisi
    /// yayınlıyor: WebSite, Organization, BreadcrumbList ve Product aynı
    /// blokta tipli düğümler olarak duruyor. Bu yüzden hem düz nesne, hem
    /// dizi, hem de @graph durumu ele alınıyor.
    /// </summary>
    private static JsonElement? SchemaOrgProductNode(HtmlDocument document)
    {
        var scripts = document.DocumentNode.SelectNodes(
            "//script[contains(translate(@type,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'ld+json')]");
        if (scripts is null)
            return null;

        foreach (var script in scripts)
        {
            JsonDocument belge;
            try
            {
                belge = JsonDocument.Parse(WebUtility.HtmlDecode(script.InnerText));
            }
            catch (JsonException)
            {
                continue;
            }

            var kok = belge.RootElement;
            IEnumerable<JsonElement> dugumler =
                kok.ValueKind == JsonValueKind.Array ? kok.EnumerateArray()
                : kok.ValueKind == JsonValueKind.Object && kok.TryGetProperty("@graph", out var graph)
                  && graph.ValueKind == JsonValueKind.Array ? graph.EnumerateArray()
                : [kok];

            foreach (var dugum in dugumler)
            {
                if (dugum.ValueKind == JsonValueKind.Object
                    && dugum.TryGetProperty("@type", out var tip)
                    && tip.ValueKind == JsonValueKind.String
                    && tip.GetString() == "Product")
                {
                    // JsonDocument burada bilinçli olarak dispose EDİLMİYOR:
                    // döndürülen JsonElement onun tamponuna bakıyor.
                    return dugum.Clone();
                }
            }
        }

        return null;
    }

    /// <summary>Product düğümünün ilk teklifi (tek nesne ya da dizi olabilir).</summary>
    private static JsonElement? SchemaOrgOffer(JsonElement product)
    {
        if (!product.TryGetProperty("offers", out var offers))
            return null;

        if (offers.ValueKind == JsonValueKind.Array)
            return offers.GetArrayLength() > 0 ? offers[0] : null;

        return offers.ValueKind == JsonValueKind.Object ? offers : null;
    }
}

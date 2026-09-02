using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.S4u;

/// <summary>
/// s4u.com.tr — yirmi beşinci kaynak. OpenCart; ürün sayfasındaki schema.org
/// Product bloğu ad, görsel, fiyat ve stok durumunu veriyor.
///
/// <b>MARKA ADI "S4U Nutrition" OLMAK ZORUNDA.</b> Bu marka katalogda ZATEN
/// var (protein7 üzerinden 5 ürün). Ad birebir aynı yazılmazsa
/// <c>ScrapeIngestionService.ResolveBrand</c> ikinci bir Brand satırı
/// yaratır — 1 Eylül'de tam bu şekilde kopya markalar oluşmuştu.
/// <c>Seller</c> ise null kalıyor (markanın kendi sitesi), böylece bayiden
/// gelen aynı ürünlerden ayrılıyorlar ve marka sayfası kendi vitrinini
/// gösterebiliyor (bkz. `7faad22`).
///
/// <b>KAPSAM KÜÇÜK, BİLEREK EKLENDİ.</b> Site 5 üründen ibaret (sitemap,
/// beş kategori sayfası ve ürün kimlikleri ayrı ayrı sayıldı, hepsi aynı 5'i
/// veriyor) — yani dört maddelik aday ölçütünden "ürün sayısı anlamlı mı"
/// maddesini GEÇMİYOR; Nutrigo tam bu sayıda elenmişti.
///
/// Üstelik 2 Eylül'de ölçüldü: beş ürünün beşinde de sitenin kendi fiyatı,
/// protein7'nin verdiği fiyatla BİREBİR AYNI (549/549/549/349/599). Yani
/// "marka fiyatı vs bayi fiyatı" karşılaştırması bir bilgi üretmiyor.
/// Bu ölçüm kullanıcıya sunuldu, markanın sitede görünmesini yine de
/// istedi — karar bilinçli.
/// </summary>
public partial class S4uScraper(HttpClient httpClient, ILogger<S4uScraper> logger) : IBrandScraper
{
    public string BrandName => "S4U Nutrition";
    public string BaseUrl => "https://s4u.com.tr";

    private const string SitemapUrl = "https://s4u.com.tr/sitemap.xml";

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(400);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchProductUrlsAsync(cancellationToken);
        if (urls.Count == 0)
            throw new InvalidOperationException("S4U: sitemap'te hiç ürün adresi bulunamadı.");

        var result = new List<ScrapedProduct>();
        var failures = 0;
        var filtered = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync(url, cancellationToken);
                var product = ParseProduct(html, url);

                if (product is null)
                    filtered++;
                else
                    result.Add(product);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        // Katalog çok küçük olduğu için oransal eşik anlamsız: bir ürünün
        // kaybı %20 demek. Tek bir hata bile tarama sonucunu şüpheli yapar.
        if (failures > 0 && result.Count == 0)
            throw new InvalidOperationException($"S4U: {urls.Count} adresin hiçbirinden ürün alınamadı.");

        logger.LogInformation(
            "S4U: {Total} adres tarandı, {Found} ürün alındı, {Filtered} süzüldü, {Failures} hata.",
            urls.Count, result.Count, filtered, failures);

        return result;
    }

    /// <summary>
    /// Sayfadaki schema.org Product bloğundan ürün. Bloğu olmayan ya da
    /// fiyatı okunamayan sayfada null döner.
    /// </summary>
    internal static ScrapedProduct? ParseProduct(string html, string url)
    {
        foreach (Match block in JsonLdRegex().Matches(html))
        {
            JsonElement root;
            try
            {
                root = JsonDocument.Parse(block.Groups[1].Value).RootElement;
            }
            catch (JsonException)
            {
                continue;
            }

            IEnumerable<JsonElement> nodes = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray()
                : new[] { root };

            foreach (var node in nodes)
            {
                if (node.ValueKind != JsonValueKind.Object)
                    continue;
                if (!node.TryGetProperty("@type", out var type) || type.GetString() != "Product")
                    continue;

                var name = node.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
                    return null;

                if (!node.TryGetProperty("offers", out var offers))
                    continue;

                var offer = offers.ValueKind == JsonValueKind.Array ? offers[0] : offers;
                if (!offer.TryGetProperty("price", out var priceNode))
                    continue;

                var raw = priceNode.ValueKind == JsonValueKind.String
                    ? priceNode.GetString()
                    : priceNode.ToString();

                // schema.org fiyatı NOKTA ondalıklı ("549.00") — Türkçe
                // biçim değil, o yüzden invariant kültürle ayrıştırılıyor.
                if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
                    || price <= 0)
                {
                    continue;
                }

                var image = node.TryGetProperty("image", out var img)
                    ? (img.ValueKind == JsonValueKind.Array ? img[0].GetString() : img.GetString())
                    : null;

                bool? inStock = null;
                if (offer.TryGetProperty("availability", out var av))
                {
                    var a = av.GetString() ?? string.Empty;
                    if (a.Contains("InStock", StringComparison.OrdinalIgnoreCase))
                        inStock = true;
                    else if (a.Contains("OutOfStock", StringComparison.OrdinalIgnoreCase))
                        inStock = false;
                }

                return new ScrapedProduct(
                    Name: name,
                    Url: url,
                    ImageUrl: string.IsNullOrWhiteSpace(image) ? null : image,
                    Category: null,
                    Price: price,
                    InStock: inStock);
            }
        }

        return null;
    }

    private async Task<List<string>> FetchProductUrlsAsync(CancellationToken cancellationToken)
    {
        var xml = await httpClient.GetStringAsync(SitemapUrl, cancellationToken);
        return LocRegex().Matches(xml)
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim())
            .Where(u => u.Contains("route=product/product", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [GeneratedRegex(@"<loc>([^<]+)</loc>", RegexOptions.IgnoreCase)]
    private static partial Regex LocRegex();

    [GeneratedRegex(@"<script[^>]*application/ld\+json[^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdRegex();
}

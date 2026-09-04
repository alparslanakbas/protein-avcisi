using System.Globalization;
using System.Net;
using System.Text.Json;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Proteinim;

/// <summary>
/// proteinim.com — otuz beşinci kaynak, BEŞİNCİ BAYİ. WooCommerce.
///
/// <b>TEK İSTEKLİK KAYNAK.</b> WooCommerce'in public Store API'si
/// (<c>/wp-json/wc/store/v1/products</c>) katalogu sayfa sayfa JSON olarak
/// veriyor; ürün başına ayrı istek GEREKMİYOR. Kayıtta <c>brands</c> alanı
/// da var, yani üretici isimden tahmin edilmiyor.
///
/// <b>FİYAT KURUŞ CİNSİNDEN GELİYOR.</b> API <c>prices.price</c> alanını
/// tam sayı olarak veriyor ve ölçeği <c>currency_minor_unit</c> söylüyor:
/// "180000" + minor_unit 2 = 1.800,00 TL. Doğrudan okunsaydı fiyatlar 100
/// kat şişerdi. Ölçek alanı sabit varsayılmıyor, kayıttan okunuyor.
///
/// <b>ÖLÇÜM (4 Eylül):</b> 53 ürün. Markalar: Olimp 21, Multipower 12,
/// Nutrend 7, Nutrever 7, Z Konzept 4, SNCK 2. İlk beşi katalogda ZATEN var
/// ("Z Konzept" takma adla "Z-Konzept"e eşleniyor); SNCK yeni.
/// </summary>
public sealed class ProteinimScraper(HttpClient httpClient, ILogger<ProteinimScraper> logger) : IBrandScraper
{
    public string BrandName => "Proteinim";
    public string BaseUrl => "https://proteinim.com";

    private const string SellerName = "proteinim.com";
    private const int PageSize = 100;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ScrapedProduct>();
        var filtered = 0;

        for (var page = 1; page <= 20; page++)
        {
            var json = await httpClient.GetStringAsync(
                $"wp-json/wc/store/v1/products?per_page={PageSize}&page={page}", cancellationToken);

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                break;

            var count = 0;
            foreach (var node in document.RootElement.EnumerateArray())
            {
                count++;
                var product = ParseProduct(node);
                if (product is null)
                    filtered++;
                else
                    result.Add(product);
            }

            if (count < PageSize)
                break;
        }

        if (result.Count == 0)
            throw new InvalidOperationException("proteinim: hiç ürün alınamadı.");

        logger.LogInformation(
            "proteinim: {Found} ürün alındı, {Filtered} takviye dışı süzüldü.", result.Count, filtered);

        return result;
    }

    internal static ScrapedProduct? ParseProduct(JsonElement node)
    {
        var name = node.TryGetProperty("name", out var n)
            ? WebUtility.HtmlDecode(n.GetString() ?? string.Empty).Trim()
            : string.Empty;
        if (name.Length == 0 || NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return null;

        if (!node.TryGetProperty("prices", out var prices) || prices.ValueKind != JsonValueKind.Object)
            return null;

        var price = ReadMinorUnitPrice(prices, "price");
        if (price is null or <= 0m)
            return null;

        // Mağazanın beyan ettiği liste fiyatı; yalnızca gerçekten yüksekse
        // "mağaza indirimi" sayılıyor.
        var regular = ReadMinorUnitPrice(prices, "regular_price");
        var storeOld = regular > price ? regular : null;

        string? brand = null;
        if (node.TryGetProperty("brands", out var brands)
            && brands.ValueKind == JsonValueKind.Array
            && brands.GetArrayLength() > 0
            && brands[0].TryGetProperty("name", out var brandName))
        {
            var raw = WebUtility.HtmlDecode(brandName.GetString() ?? string.Empty).Trim();
            if (raw.Length > 0)
                brand = BrandNameNormalizer.Normalize(raw);
        }

        string? image = null;
        if (node.TryGetProperty("images", out var images)
            && images.ValueKind == JsonValueKind.Array
            && images.GetArrayLength() > 0
            && images[0].TryGetProperty("src", out var src))
        {
            image = src.GetString();
        }

        bool? inStock = node.TryGetProperty("is_in_stock", out var stock)
            && stock.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? stock.GetBoolean()
            : null;

        var url = node.TryGetProperty("permalink", out var link) ? link.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return new ScrapedProduct(
            Name: name,
            Url: url,
            ImageUrl: string.IsNullOrWhiteSpace(image) ? null : image,
            // Kaynağın kategorileri bizim slug'larımız değil; kategori
            // ürün adından çıkarılıyor.
            Category: null,
            Price: price.Value,
            StoreOldPrice: storeOld,
            BrandName: brand,
            InStock: inStock,
            Seller: SellerName);
    }

    /// <summary>
    /// Kuruş cinsinden gelen fiyatı gerçek tutara çevirir.
    /// Ölçek <c>currency_minor_unit</c>'ten OKUNUYOR, sabit varsayılmıyor.
    /// </summary>
    private static decimal? ReadMinorUnitPrice(JsonElement prices, string field)
    {
        if (!prices.TryGetProperty(field, out var value))
            return null;

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        if (!decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor))
            return null;

        var unit = prices.TryGetProperty("currency_minor_unit", out var u) && u.TryGetInt32(out var parsed)
            ? Math.Clamp(parsed, 0, 6)
            : 2;

        return minor / (decimal)Math.Pow(10, unit);
    }
}

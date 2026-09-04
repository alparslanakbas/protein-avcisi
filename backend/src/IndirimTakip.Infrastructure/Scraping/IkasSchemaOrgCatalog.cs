using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// ikas mağazalarının ÜRÜN SAYFASINDAKİ schema.org bloğundan ürün okuyan
/// ortak yardımcı.
///
/// <b>NEDEN GraphQL DEĞİL.</b> ikas kaynaklarımızın çoğu (GNC, ProteinOcean,
/// Heyday, Think Nutrition, Imperium) storefront GraphQL API'sini kullanıyor
/// ve tek istekte katalogu alıyor. Gigi's ve MLA Protein'de aynı uç denendi:
/// istek başarılı oluyor ama <c>totalCount: 0</c> dönüyor — bu mağazalarda
/// ürünler o satış kanalında görünmüyor. Kovalamak yerine sitenin
/// <c>products.xml</c> sitemap'i + ürün sayfasındaki schema.org bloğu
/// kullanıldı; ikisi de SUNUCU TARAFINDA render ediliyor (curl ile
/// doğrulandı, JS gerekmiyor) ve 79/79 ile 86/86 ürün hatasız alındı.
///
/// Maliyet ürün başına bir istek. Katalogların küçük olması (79 ve 86) bunu
/// kabul edilebilir kılıyor — protein7 (~900) ve Provitamin (~430) gibi
/// <c>DailyOnly</c> olmayı gerektirmiyor.
/// </summary>
public static partial class IkasSchemaOrgCatalog
{
    /// <summary>Sitemap'ten ürün adreslerini çıkarır.</summary>
    public static List<string> ParseSitemap(string xml) =>
        LocRegex().Matches(xml)
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Ürün sayfasındaki schema.org <c>Product</c> bloğundan ürün.
    ///
    /// <paramref name="brandNameOverride"/> verilirse ürünün markası o olur;
    /// verilmezse schema.org'daki marka adı kullanılır. Çok markalı bir
    /// mağazada (MLA Protein kendi ürünlerinin yanında Nutraxin, Dr. Pan,
    /// Fitnut da satıyor) ikincisi gerekiyor.
    /// </summary>
    public static ScrapedProduct? ParseProduct(string html, string url, string? brandNameOverride = null)
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

            // Üç biçim de destekleniyor: düz nesne, dizi ve @graph.
            //
            // @graph, WebSite/Organization/BreadcrumbList/Product düğümlerini
            // tek blokta listeleyen yaygın bir kalıp; Muscle Pump ve
            // proteinpazari bunu kullanıyor. Düz nesne varsayan bir okuyucu
            // Product'ı hiç bulamaz — Muscle Pump'ta canlıda tam bu oldu
            // (site mikro veriden JSON-LD'ye geçince tarama 0 ürün verdi).
            IEnumerable<JsonElement> nodes =
                root.ValueKind == JsonValueKind.Array ? root.EnumerateArray()
                : root.ValueKind == JsonValueKind.Object
                  && root.TryGetProperty("@graph", out var graph)
                  && graph.ValueKind == JsonValueKind.Array ? graph.EnumerateArray()
                : [root];

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

                var offer = offers.ValueKind == JsonValueKind.Array
                    ? (offers.GetArrayLength() > 0 ? offers[0] : default)
                    : offers;
                if (offer.ValueKind != JsonValueKind.Object || !offer.TryGetProperty("price", out var priceNode))
                    continue;

                var raw = priceNode.ValueKind == JsonValueKind.String
                    ? priceNode.GetString()
                    : priceNode.ToString();

                // schema.org fiyatı NOKTA ondalıklı ("755.00"); Türkçe kültürle
                // ayrıştırılsaydı 75500 çıkardı. Bu tuzağa projede düşüldü.
                //
                // Fiyatı 0 olan kayıtlar da burada eleniyor: Gigi's'te
                // "Kendi Paketini Kendin Yap" bir yapılandırıcı sayfası,
                // gerçek bir ürün değil ve 0 TL ile geliyor.
                if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
                    || price <= 0)
                {
                    continue;
                }

                var image = node.TryGetProperty("image", out var img)
                    ? (img.ValueKind == JsonValueKind.Array
                        ? (img.GetArrayLength() > 0 ? img[0].GetString() : null)
                        : img.GetString())
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

                var brand = brandNameOverride;
                if (brand is null && node.TryGetProperty("brand", out var b)
                    && b.ValueKind == JsonValueKind.Object
                    && b.TryGetProperty("name", out var bn))
                {
                    var okunan = bn.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(okunan))
                        brand = BrandNameNormalizer.Normalize(okunan);
                }

                return new ScrapedProduct(
                    Name: name,
                    Url: url,
                    ImageUrl: string.IsNullOrWhiteSpace(image) ? null : image,
                    // Kaynağın kendi kategorisi bizim slug'larımıza oturmuyor;
                    // kategori ürün adından çıkarılıyor.
                    Category: null,
                    Price: price,
                    BrandName: brand,
                    InStock: inStock);
            }
        }

        return null;
    }

    [GeneratedRegex(@"<loc>([^<]+)</loc>", RegexOptions.IgnoreCase)]
    private static partial Regex LocRegex();

    [GeneratedRegex(@"<script[^>]*application/ld\+json[^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdRegex();
}

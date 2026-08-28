using System.Text.RegularExpressions;
using System.Web;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Supplementler;

/// <summary>
/// Supplementler.com — diğer scraper'ların aksine bir MARKA değil, çok
/// markalı bir bayi. Bu yüzden ürünler "Supplementler" altında değil, kendi
/// ÜRETİCİ markaları altında kaydediliyor (Optimum Nutrition, Scitec, BSN…);
/// mağaza bağlantısı ise ürünün satıldığı yere, supplementler.com'a gidiyor.
/// Marka bilgisi kartın kendi veri özniteliğinden geliyor, tahmin değil.
///
/// Site nopCommerce üzerinde çalışıyor. Kategori listesindeki kartlar ürün
/// adını, gerçek markayı ve fiyatı öznitelik olarak taşıdığı için ürün detay
/// sayfasına gitmeye gerek yok.
/// </summary>
public partial class SupplementlerScraper(HttpClient httpClient) : IBrandScraper
{
    /// <summary>
    /// Yalnızca sitenin KENDİ markalı ürünleri bu ad altında toplanıyor;
    /// diğer ürünler kendi üreticilerine yazılıyor.
    /// </summary>
    public string BrandName => "Supplementler";

    public string BaseUrl => "https://www.supplementler.com";

    /// <summary>
    /// Kendi mağazasından zaten doğrudan takip ettiğimiz markalar buradan
    /// ALINMIYOR. Alınsaydı aynı ürün iki ayrı kayıt olarak görünürdü (iki
    /// farklı adres, iki farklı fiyat) ve listede yinelenmiş gibi dururdu.
    /// Üreticinin kendi mağazası fiyat geçmişi için daha doğrudan bir kaynak.
    /// </summary>
    private static readonly string[] AlreadyTrackedBrands = ["hardline", "bigjoy", "big joy"];

    /// <summary>Sayfa başına 40 ürün geliyor; bu tavan sonsuz döngüye karşı.</summary>
    private const int MaxPagesPerCategory = 40;

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(400);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // Bir ürün birden fazla kategoride görünüyor; adrese göre
        // tekilleştiriliyor ve ilk eşleşen kategori kazanıyor.
        var products = new Dictionary<string, ScrapedProduct>();

        foreach (var (path, category) in ListingPaths())
        {
            var previousPageUrls = new HashSet<string>();

            for (var page = 1; page <= MaxPagesPerCategory; page++)
            {
                var url = path + (page == 1 ? "" : $"?pagenumber={page}");

                string html;
                try
                {
                    html = await httpClient.GetStringAsync(url, cancellationToken);
                }
                catch (HttpRequestException)
                {
                    // Tek bir sayfa tüm taramayı düşürmemeli.
                    break;
                }

                await Task.Delay(DelayBetweenRequests, cancellationToken);

                var cards = ProductCardRegex().Matches(html);
                if (cards.Count == 0)
                    break;

                // Sayfalamanın gerçekten ilerlediği, bu sayfanın adres kümesi
                // bir öncekiyle karşılaştırılarak anlaşılıyor.
                var pageUrls = new HashSet<string>();

                foreach (Match card in cards)
                {
                    var productUrl = HttpUtility.HtmlDecode(card.Groups["url"].Value).Trim();
                    if (!productUrl.StartsWith("http"))
                        continue;

                    pageUrls.Add(productUrl);
                    if (products.ContainsKey(productUrl))
                        continue;

                    var name = HttpUtility.HtmlDecode(card.Groups["name"].Value).Trim();
                    var brand = HttpUtility.HtmlDecode(card.Groups["brand"].Value).Trim();
                    if (name.Length == 0 || brand.Length == 0)
                        continue;

                    if (IsAlreadyTracked(brand) || NonSupplementProductFilter.IsAccessoryOrApparel(name))
                        continue;

                    var price = ParsePrice(card.Groups["price"].Value);
                    if (price is null or <= 0)
                        continue;

                    products[productUrl] = new ScrapedProduct(
                        Name: name,
                        Url: productUrl,
                        ImageUrl: null,
                        Category: category,
                        Price: price.Value,
                        BrandName: brand);
                }

                if (pageUrls.SetEquals(previousPageUrls))
                    break;

                previousPageUrls = pageUrls;
            }
        }

        return products.Values.ToList();
    }

    /// <summary>
    /// Taranacak liste sayfaları: kategori sayfalarının kendisi.
    ///
    /// Marka × kategori sayfalarını (site haritasından türetilerek) gezmek de
    /// denendi ve ÖLÇÜLDÜ: aynı 545 ürünü verdi ama süre 214 saniyeden 1302
    /// saniyeye çıktı. Marka sayfaları kategori sayfasının alt kümesi
    /// olduğundan ek kapsam getirmiyor; düz kategori listesi yeterli.
    /// </summary>
    private static List<(string Path, string? Category)> ListingPaths() =>
        SupplementlerCategories.All
            .Select(c => (SupplementlerCategories.Path(c.Slug, c.Id), c.Category))
            .ToList();

    private static bool IsAlreadyTracked(string brand)
    {
        var normalized = brand.Replace(" ", "");
        return AlreadyTrackedBrands.Any(b =>
            normalized.Contains(b.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Öznitelikteki fiyat "8950,0" biçiminde — binlik ayracı yok, ondalık
    /// virgül. Türkçe fiyat ayrıştırıcısı "1.234,56" kalıbını beklediği için
    /// burada ayrıca ele alınıyor.
    /// </summary>
    private static decimal? ParsePrice(string raw)
    {
        var text = raw.Trim().Replace(".", "").Replace(',', '.');
        return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Kart bağlantısı; ad, marka ve fiyat öznitelik olarak duruyor. Sıra
    /// sabit olduğu için tek desenle okunuyor.
    /// </summary>
    [GeneratedRegex(
        """href="(?<url>https://www\.supplementler\.com/urun/[^"]+)"[^>]*?data-name="(?<name>[^"]*)"[^>]*?data-brand="(?<brand>[^"]*)"[^>]*?data-price="(?<price>[^"]*)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex ProductCardRegex();


}

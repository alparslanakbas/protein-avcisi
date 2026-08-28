using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.West;

/// <summary>
/// West Nutrition (IdeaSoft altyapısı — projede ilk kez).
///
/// Kategori sayfalarından okunuyor. Ürün DETAY sayfası bilinçli olarak
/// kullanılmadı: oradaki fiyat işaretlemesi tutarsız (indirimsiz üründe tek
/// fiyat "product-price-old" sınıfıyla geliyor, indirimli üründe o blok hiç
/// yok) ve sayfada "benzer ürünler" widget'ı da fiyat taşıdığı için yanlış
/// ürünün fiyatını kaydetme riski var. Kategori listesindeki
/// showcase-price-new / showcase-price-old ikilisi ise tutarlı ve
/// SSN'deki OpenCart kalıbının aynısı.
/// </summary>
public class WestNutritionScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "West Nutrition";
    public string BaseUrl => "https://www.westnutrition.com.tr";

    /// <summary>
    /// Taranacak kategoriler ve bizim kategori slug'ımıza eşlemesi. Sitedeki
    /// kategori adları bizimkilerle büyük ölçüde örtüştüğü için kategori
    /// isimden TAHMİN edilmiyor, doğrudan kaynaktan alınıyor.
    ///
    /// Kapsam dışı bırakılanlar (bilinçli): "aksesuarlar" ve
    /// "sporcu-bakim-urunleri" projenin kapsamı değil; "markalar" ve
    /// "herbina" West'in sattığı BAŞKA markaların vitrinleri, bizim marka
    /// alanımızı yanlışlarlardı.
    /// </summary>
    private static readonly (string Slug, string? Category)[] Categories =
    [
        ("protein-tozu", "protein-tozu"),
        ("protein-barlar", "saglikli-atistirmaliklar"),
        ("protein-zamani", "protein-tozu"),
        ("mass-gainer", "kilo-hacim"),
        ("kreatin", "kreatin"),
        ("pre-workout", "pre-workout"),
        ("bcaa-amino-asit", "amino-asitler"),
        ("arjinin", "amino-asitler"),
        ("l-karnitin", "l-carnitine-cla"),
        ("ogun-tozu", "kilo-hacim"),
        // Bu üçünde karışık ürün var; kategori isimden çıkarılsın diye null.
        ("takviye-edici-gidalar", null),
        ("kompleks-urunler", null),
        ("vegan", null),
        ("supplement-paketleri", null),
        ("firsatlar-indirim", null),
        ("saglikli-yasam-urunleri", null),
    ];

    /// <summary>
    /// West kendi sitesinde başka markaların ürünlerini de satıyor; bunları
    /// almak ürünü "West Nutrition" markası altında göstermek olurdu.
    /// </summary>
    private static readonly string[] OtherBrandPrefixes = ["herbina"];

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // Bir ürün birden fazla kategoride görünebiliyor (ör. "fırsatlar"),
        // adrese göre tekilleştiriliyor.
        var products = new Dictionary<string, ScrapedProduct>();

        foreach (var (slug, category) in Categories)
        {
            string html;
            try
            {
                html = await httpClient.GetStringAsync($"/kategori/{slug}", cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Kategori kaldırılmış olabilir; tek bir kategori tüm taramayı
                // düşürmemeli.
                continue;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Yalnızca ana ızgara: sayfada ayrıca "öne çıkanlar" widget'ı var
            // (featured-showcase-*) ve o da fiyat taşıyor.
            var titleNodes = doc.DocumentNode.SelectNodes(
                "//div[@class='showcase-title']/a[starts-with(@href,'/urun/')]");
            if (titleNodes is null)
                continue;

            foreach (var titleNode in titleNodes)
            {
                var card = titleNode.Ancestors("div")
                    .FirstOrDefault(d => d.GetAttributeValue("class", "") == "showcase");
                if (card is null)
                    continue;

                var href = titleNode.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href))
                    continue;

                var url = href.StartsWith("http") ? href : BaseUrl + href;
                if (products.ContainsKey(url))
                    continue;

                var name = HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
                if (string.IsNullOrEmpty(name) || NonSupplementProductFilter.IsAccessoryOrApparel(name))
                    continue;

                if (OtherBrandPrefixes.Any(b => name.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var priceNode = card.SelectSingleNode(".//div[@class='showcase-price-new']");
                if (priceNode is null)
                    continue;

                decimal price;
                try
                {
                    price = TurkishPriceParser.Parse(priceNode.InnerText);
                }
                catch (FormatException)
                {
                    continue;
                }

                // price-old sadece indirim varsa basılıyor — markanın kendi
                // beyan ettiği eski fiyat, "Mağaza İndirimi" için ayrı tutulur.
                var oldNode = card.SelectSingleNode(".//div[@class='showcase-price-old']");
                decimal? storeOld = null;
                if (oldNode is not null)
                {
                    try { storeOld = TurkishPriceParser.Parse(oldNode.InnerText); }
                    catch (FormatException) { storeOld = null; }
                }

                var imgNode = card.SelectSingleNode(".//div[@class='showcase-image']//img");
                var imageUrl = imgNode?.Attributes["data-src"]?.Value ?? imgNode?.Attributes["src"]?.Value;
                if (imageUrl is not null && imageUrl.StartsWith("//"))
                    imageUrl = "https:" + imageUrl;

                products[url] = new ScrapedProduct(
                    Name: name,
                    Url: url,
                    ImageUrl: imageUrl,
                    Category: category,
                    Price: price,
                    StoreOldPrice: storeOld);
            }
        }

        return products.Values.ToList();
    }
}

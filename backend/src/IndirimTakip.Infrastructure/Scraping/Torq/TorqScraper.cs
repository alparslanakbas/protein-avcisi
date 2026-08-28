using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Torq;

/// <summary>
/// Torq Nutrition (OpenCart). SSN ile aynı altyapı ama kategori kategori
/// gezmeye gerek yok: arama rotası boş sorguyla tüm katalogu tek listede
/// veriyor, bu yüzden hem daha az istek atıyoruz hem de kategori listesini
/// elle güncel tutma yükü doğmuyor.
/// </summary>
/// Not: IProductDetailFetcher BİLİNÇLİ olarak uygulanmadı. Torq'un ürün
/// detay sayfasında açıklama tablosu boş geliyor (içerik muhtemelen sonradan
/// yükleniyor); boş dönen bir çekici, detay tamamlama servisinin "kontrol
/// edildi" damgasını atmasına ve o ürünün bir daha hiç denenmemesine yol
/// açardı — hiç uygulamamaktan kötü olurdu. İçeriğin nereden geldiği
/// çözülünce eklenebilir.
public class TorqScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Torq Nutrition";
    public string BaseUrl => "https://www.torqnutrition.com.tr";

    /// <summary>Sunucunun kabul ettiği en büyük sayfa boyutu; 266 ürün 3 istekte iniyor.</summary>
    private const int PageSize = 100;

    /// <summary>Beklenmedik bir sayfalama davranışında sonsuz döngüye girmemek için.</summary>
    private const int MaxPages = 30;

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Torq kendi sitesinde başka markaların ürünlerini de satıyor. Bunları
    /// almak, ürünü "Torq Nutrition" markası altında göstermek olurdu — yanlış
    /// veri. Marka adı ürün adının başında geçtiği için ada göre eleniyor.
    /// Yeni bir tedarikçi görülürse buraya eklenir.
    /// </summary>
    private static readonly string[] OtherBrandPrefixes =
    [
        "solgar", "nature's bounty", "natures bounty", "nutripure", "ocean ", "nutraxin",
    ];

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var products = new Dictionary<string, ScrapedProduct>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var path = $"/index.php?route=product/search&search=&limit={PageSize}&page={page}";
            var html = await httpClient.GetStringAsync(path, cancellationToken);
            await Task.Delay(DelayBetweenRequests, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var productNodes = doc.DocumentNode.SelectNodes(
                "//div[contains(concat(' ', normalize-space(@class), ' '), ' product-thumb ')]");
            if (productNodes is null || productNodes.Count == 0)
                break;

            var newOnThisPage = 0;

            foreach (var node in productNodes)
            {
                // Ürün adı ve adresi başlıktaki bağlantıda; görseldeki bağlantı
                // aynı adrese gidiyor ama metni boş.
                var linkNode = node.SelectSingleNode(".//h4/a");
                var priceNode = node.SelectSingleNode(".//p[contains(@class,'price')]");
                if (linkNode is null || priceNode is null)
                    continue;

                var url = NormalizeUrl(linkNode.GetAttributeValue("href", ""));
                if (string.IsNullOrEmpty(url) || products.ContainsKey(url))
                    continue;

                var name = HtmlEntity.DeEntitize(linkNode.InnerText).Trim();
                if (string.IsNullOrEmpty(name) || NonSupplementProductFilter.IsAccessoryOrApparel(name))
                    continue;

                if (OtherBrandPrefixes.Any(b => name.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var imgNode = node.SelectSingleNode(".//img");
                var imageUrl = imgNode?.Attributes["data-src"]?.Value ?? imgNode?.Attributes["src"]?.Value;

                // İki fiyat biçimi var: indirim yoksa p.price'ın düz metni,
                // varsa içinde price-old + price-new span'leri. Metindeki son
                // fiyat her iki durumda da güncel fiyat, sondan bir önceki
                // (varsa) mağazanın beyan ettiği eski fiyat — Hardline'da da
                // kullanılan aynı ayrıştırıcı bunu tek yoldan çözüyor.
                decimal current;
                decimal? storeOld;
                try
                {
                    (current, storeOld) = TurkishPriceParser.ParsePricePair(priceNode.InnerText);
                }
                catch (FormatException)
                {
                    // Fiyatı okunamayan tek bir kart yüzünden tüm tarama düşmemeli
                    // (ör. "Fiyat için arayınız" gibi bir kart).
                    continue;
                }

                products[url] = new ScrapedProduct(
                    Name: name,
                    Url: url,
                    ImageUrl: imageUrl,
                    // Kategori ürün adından çıkarılıyor (ProductAttributeParser),
                    // arama rotası kategori bilgisi vermiyor.
                    Category: null,
                    Price: current,
                    StoreOldPrice: storeOld);

                newOnThisPage++;
            }

            // Sayfa doldu ama hepsi zaten elimizdeyse sayfalama dönmüş demektir.
            if (newOnThisPage == 0)
                break;
        }

        return products.Values.ToList();
    }

    /// <summary>
    /// Liste sayfasındaki bağlantılar arama parametrelerini taşıyor
    /// (?search=&amp;limit=100). Bunlar adresin bir parçası değil; temizlenmezse
    /// aynı ürün farklı sayfa boyutlarında farklı kayıt olarak görünür ve
    /// ortaklık takip parametresi de bunların arkasına eklenir.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var queryIndex = url.IndexOf('?');
        return queryIndex >= 0 ? url[..queryIndex] : url;
    }
}

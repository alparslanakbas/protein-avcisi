using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Ssn;

public class SsnScraper(HttpClient httpClient) : IBrandScraper
{
    // Aksesuar/ekipman değil, sadece takviye kategorileri (projenin kapsamı).
    private static readonly string[] CategorySlugs =
    [
        "protein-tozu",
        "kilo-hacim",
        "amino-asitler",
        "kreatin",
        "l-carnitine-cla",
        "vitamin",
        "pre-workout",
        "saglikli-atistirmaliklar",
    ];

    public string BrandName => "SSN";
    public string BaseUrl => "https://www.ssnsports.com.tr";

    // Kategori/sayfa istekleri arası nezaket beklemesi: siteyi yormamak, IP engellenme riskini azaltmak için.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // Bir ürün birden fazla kategoride görünebiliyor (örn. kombinasyon paketleri); Url'e göre dedupe ediyoruz.
        var products = new Dictionary<string, ScrapedProduct>();

        foreach (var slug in CategorySlugs)
        {
            for (var page = 1; ; page++)
            {
                var path = page == 1 ? $"/{slug}" : $"/{slug}/page-{page}";
                var html = await httpClient.GetStringAsync(path, cancellationToken);
                await Task.Delay(DelayBetweenRequests, cancellationToken);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var productNodes = doc.DocumentNode.SelectNodes(
                    "//div[contains(concat(' ', normalize-space(@class), ' '), ' product-thumb ')]");
                if (productNodes is null || productNodes.Count == 0)
                    break;

                foreach (var node in productNodes)
                {
                    var linkNode = node.SelectSingleNode(".//div[contains(@class,'name')]/a");
                    var priceNode = node.SelectSingleNode(".//span[contains(@class,'price-new')]");
                    if (linkNode is null || priceNode is null)
                        continue;

                    var url = linkNode.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(url) || products.ContainsKey(url))
                        continue;

                    var imgNode = node.SelectSingleNode(".//img");
                    var imageUrl = imgNode?.Attributes["data-src"]?.Value ?? imgNode?.Attributes["src"]?.Value;

                    // price-old sadece indirim varsa ekleniyor — mağazanın kendi beyan
                    // ettiği eski fiyat, "Mağaza İndirimi" için ayrı tutuluyor.
                    var priceOldNode = node.SelectSingleNode(".//span[contains(@class,'price-old')]");

                    products[url] = new ScrapedProduct(
                        Name: HtmlEntity.DeEntitize(linkNode.InnerText).Trim(),
                        Url: url,
                        ImageUrl: imageUrl,
                        Category: slug,
                        Price: TurkishPriceParser.Parse(priceNode.InnerText),
                        StoreOldPrice: priceOldNode is not null ? TurkishPriceParser.Parse(priceOldNode.InnerText) : null);
                }
            }
        }

        return products.Values.ToList();
    }
}

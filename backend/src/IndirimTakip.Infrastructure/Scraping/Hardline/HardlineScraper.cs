using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Hardline;

public class HardlineScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Hardline";
    public string BaseUrl => "https://www.hardlinenutrition.com";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // /urunler, sayfalama olmadan tüm kataloğu tek seferde veriyor (182 ürün, doğrulandı).
        var html = await httpClient.GetStringAsync("/urunler", cancellationToken);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var productNodes = doc.DocumentNode.SelectNodes(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' product-thumb ')]");
        if (productNodes is null)
            return [];

        var products = new List<ScrapedProduct>();
        foreach (var node in productNodes)
        {
            // /kampanyalar'da h4/a, /urunler'de div.text-center/a kullanılıyor; caption
            // içindeki ilk link'i almak her iki yapıda da güvenilir çalışıyor.
            var linkNode = node.SelectSingleNode(".//div[contains(@class,'caption')]//a[1]");
            var priceContainer = node.SelectSingleNode(".//p[contains(@class,'price')]");
            if (linkNode is null || priceContainer is null)
                continue;

            var url = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(url))
                continue;

            var imgNode = node.SelectSingleNode(".//img");
            var (price, storeOldPrice) = TurkishPriceParser.ParsePricePair(priceContainer.InnerText);

            products.Add(new ScrapedProduct(
                Name: HtmlEntity.DeEntitize(linkNode.InnerText).Trim(),
                Url: url,
                ImageUrl: imgNode?.Attributes["src"]?.Value,
                Category: null,
                Price: price,
                StoreOldPrice: storeOldPrice));
        }

        return products;
    }
}

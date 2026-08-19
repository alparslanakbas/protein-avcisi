using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Hardline;

public class HardlineScraper(HttpClient httpClient) : IBrandScraper, IProductDescriptionFetcher
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

    // Ürün açıklaması sadece DETAY sayfasında, iki ayrı bölümde geliyor:
    // "ozet-yazisi" ("... NEDİR?" kısa tanıtım) ve "aciklama_txt" ("...
    // AÇIKLAMA"/"... NASIL KULLANILIR?" gibi h2 başlıklı bölümler). Paket/
    // kombinasyon ürünlerinde ikisi de boş/yok olabiliyor — bu durumda null
    // dönüyoruz (uydurma yok).
    public async Task<string?> FetchDescriptionAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var sections = new List<string>();

        var summaryNode = doc.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' ozet-yazisi ')]");
        if (summaryNode is not null)
            sections.AddRange(ExtractH2Sections(summaryNode));

        var detailNode = doc.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' aciklama_txt ')]");
        if (detailNode is not null)
            sections.AddRange(ExtractH2Sections(detailNode));

        return sections.Count == 0 ? null : string.Join("\n\n", sections);
    }

    // Konteynerin doğrudan çocukları arasında h2 başlık + ardından gelen p'leri
    // "Başlık: metin" olarak grupluyor (HIQ/ProteinOcean'daki aynı desen).
    private static IEnumerable<string> ExtractH2Sections(HtmlNode container)
    {
        string? currentTitle = null;
        var buffer = new List<string>();

        foreach (var child in container.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element)
                continue;

            if (child.Name == "h2")
            {
                if (currentTitle is not null && buffer.Count > 0)
                    yield return $"{currentTitle}: {string.Join(" ", buffer)}";

                currentTitle = HtmlEntity.DeEntitize(child.InnerText).Trim();
                buffer = [];
            }
            else if (child.Name == "p")
            {
                var text = HtmlEntity.DeEntitize(child.InnerText).Trim();
                if (text.Length > 0)
                    buffer.Add(text);
            }
        }

        if (currentTitle is not null && buffer.Count > 0)
            yield return $"{currentTitle}: {string.Join(" ", buffer)}";
    }
}

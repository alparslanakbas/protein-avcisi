using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Hiq;

public partial class HiqScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "HIQ";
    public string BaseUrl => "https://takehiq.com";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ScrapedProduct>();

        for (var page = 1; ; page++)
        {
            var response = await httpClient.GetFromJsonAsync<ShopifyProductsResponse>(
                $"products.json?limit=250&page={page}", JsonOptions, cancellationToken);

            if (response is null || response.Products.Count == 0)
                break;

            foreach (var product in response.Products)
            {
                var variant = product.Variants.Find(v => v.Available);
                if (variant is null)
                    continue;

                // Site kapsamı spor takviyesi/protein — HIQ mağazasında tişört/
                // hoodie ("type:wearable") ve shaker/ekipman ("type:equipment")
                // gibi takviye dışı ürünler de satılıyor, kendi etiketleriyle
                // (tags) açıkça işaretli. Bunlar kullanıcı isteğiyle scrape
                // edilmiyor — bir shaker'ın "indirimi" bu sitenin amacına
                // (gerçek takviye fiyat takibi) hizmet etmiyor. "Başlangıç
                // Paketi + Shaker" gibi gerçek takviye paketleri "type:amino"/
                // "type:protein"/"type:preworkout" taşıdığı için etkilenmiyor.
                if (product.Tags.Any(t => t is "type:wearable" or "type:equipment"))
                    continue;

                var nutritionJson = ExtractNutritionJson(product.BodyHtml);

                result.Add(new ScrapedProduct(
                    Name: product.Title,
                    Url: $"https://takehiq.com/products/{product.Handle}",
                    ImageUrl: product.Images.Count > 0 ? product.Images[0].Src : null,
                    Category: string.IsNullOrWhiteSpace(product.ProductType) ? null : product.ProductType,
                    Price: variant.Price,
                    ServingSizeGrams: ExtractServingSizeGrams(product.BodyHtml),
                    StoreOldPrice: variant.CompareAtPrice > variant.Price ? variant.CompareAtPrice : null,
                    Description: ExtractDescription(product.BodyHtml),
                    NutritionJson: nutritionJson,
                    ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson)));
            }

            if (response.Products.Count < 250)
                break;
        }

        return result;
    }

    // HIQ'nun ürün açıklamasında (body_html) gerçek bir besin değeri tablosu
    // geliyor: <table class="nutrition-table"><tr><th>Bileşen</th><th>100 g</th>
    // <th>4 g</th></tr>...</table> — son başlık hücresi porsiyon büyüklüğü.
    // Tabloda gram cinsinden değilse (kapsül/tablet ürünler gibi) null döneriz;
    // uydurmak yerine boş bırakmayı tercih ediyoruz.
    private static decimal? ExtractServingSizeGrams(string? bodyHtml)
    {
        if (string.IsNullOrEmpty(bodyHtml))
            return null;

        var tableIndex = bodyHtml.IndexOf("nutrition-table", StringComparison.Ordinal);
        if (tableIndex < 0)
            return null;

        var headerMatch = HeaderRowRegex().Match(bodyHtml[tableIndex..]);
        if (!headerMatch.Success)
            return null;

        var headerCells = HeaderCellRegex().Matches(headerMatch.Value)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        if (headerCells.Count < 2)
            return null;

        var gramMatch = ServingGramsRegex().Match(headerCells[^1]);
        if (!gramMatch.Success)
            return null;

        return decimal.Parse(gramMatch.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"<tr>\s*(<th>.*?</th>\s*){2,}</tr>", RegexOptions.Singleline)]
    private static partial Regex HeaderRowRegex();

    [GeneratedRegex(@"<th>(.*?)</th>")]
    private static partial Regex HeaderCellRegex();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*g$", RegexOptions.IgnoreCase)]
    private static partial Regex ServingGramsRegex();

    // Aynı nutrition-table'ı (yukarıdaki porsiyon büyüklüğü çıkarımıyla aynı
    // kaynak) tam besin değeri olarak da yakalıyoruz — tablo "Bileşen | 100 g
    // | porsiyon" şeklinde 3 sütunlu, SON sütun gerçek porsiyon başına değer
    // (gerçek bir üründe doğrulandı). Tabloyu SADECE nutrition-table class'ına
    // scope'layıp sayfadaki başka tabloları (varsa) karıştırmıyoruz.
    private static string? ExtractNutritionJson(string? bodyHtml)
    {
        if (string.IsNullOrEmpty(bodyHtml))
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(bodyHtml);

        var table = doc.DocumentNode.SelectSingleNode(
            "//table[contains(concat(' ', normalize-space(@class), ' '), ' nutrition-table ')]");
        return table is null ? null : NutritionParser.BuildNutritionJson(HtmlNutritionExtractor.FromTables(table));
    }

    // Aynı besin değeri kutusunda "Açıklama"/"İçindekiler"/"Kullanım Talimatı"
    // bölümleri hep <div class="nutrition-title">BAŞLIK</div><div
    // class="nutrition-text">METİN</div> çifti şeklinde geliyor — "Besin Değeri"
    // (nutrition-note + table kullanıyor) ve "Uyarı" (nutrition-warning-*
    // class'ları) bu desenle hiç eşleşmediği için doğal olarak dışarıda kalıyor,
    // ayrıca filtrelemeye gerek yok.
    private static string? ExtractDescription(string? bodyHtml)
    {
        if (string.IsNullOrEmpty(bodyHtml))
            return null;

        var sections = DescriptionSectionRegex().Matches(bodyHtml)
            .Select(m => $"{StripHtml(m.Groups[1].Value)}: {StripHtml(m.Groups[2].Value)}")
            .Where(s => s.Length > 2)
            .ToList();

        return sections.Count == 0 ? null : string.Join("\n\n", sections);
    }

    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(TagRegex().Replace(html, " ")).Trim().Replace("  ", " ");

    [GeneratedRegex(@"<div class=""nutrition-title"">(.*?)</div>\s*<div class=""nutrition-text"">(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex DescriptionSectionRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
}

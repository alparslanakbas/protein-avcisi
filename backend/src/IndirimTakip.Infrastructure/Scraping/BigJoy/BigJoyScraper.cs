using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.BigJoy;

/// <summary>
/// BigJoy — Nuxt tabanlı bir SPA, sayfa kaynağında fiyat yok. Sitenin kendi
/// arka uç ucu kullanılıyor: <c>POST /api/product-category</c>, form-encoded
/// gövde ile. Uç, tarayıcı gezilirken ağ trafiği izlenerek bulundu
/// (ProteinOcean'da işe yarayan aynı yöntem); tahminle bulunamıyordu çünkü
/// bilinen tüm yollar SPA kabuğunu döndürüyor.
///
/// Yanıt zengin: fiyat, indirimli fiyat, gramaj, aroma, üretici, açıklama ve
/// görsel tek istekte geliyor — ürün detay sayfasına ayrıca gitmeye gerek yok.
/// </summary>
public partial class BigJoyScraper(HttpClient httpClient) : IBrandScraper, IProductDetailFetcher
{
    public string BrandName => "BigJoy";
    public string BaseUrl => "https://www.bigjoy.com.tr";

    /// <summary>
    /// Kategori kimliği ve bizim slug'ımıza eşlemesi. Kimlikler sitenin
    /// <c>/api/category-menu</c> ucundan alındı. Anlamı belirsiz olanlar
    /// (Performans ve Güç, Endurance, Avantajlı Paketler) bilinçli olarak
    /// null: yanlış kategori, kategorisiz kalmaktan kötü — isimden çıkarıma
    /// bırakılıyor.
    /// </summary>
    private static readonly (int Id, string? Category)[] Categories =
    [
        // Sıra önemli: bir ürün birden fazla kategoride görünebiliyor ve ilk
        // eşleşen kazanıyor. Mağaza gainer'ları hem "Kilo ve Hacim" hem
        // "Protein Tozu" altında listeliyor; dar kategoriler önce geliyor ki
        // Mass Attack gibi ürünler protein tozu sayılmasın.
        (1001, "kreatin"),
        (729, "l-carnitine-cla"),
        (734, "amino-asitler"),
        (748, "kilo-hacim"),
        (745, "vitamin"),
        (731, "saglikli-atistirmaliklar"),
        (754, "protein-tozu"),
        (736, null),  // Performans ve Güç
        (999, null),  // Endurance (Dayanıklılık)
        (878, null),  // Avantajlı Paketler
    ];

    /// <summary>
    /// BigJoy kendi sitesinde başka markaları da satıyor (ONTHEGO, Mealjoy,
    /// ZeroSHOT gibi). Onları almak ürünü "BigJoy" markası altında göstermek
    /// olurdu; yanıttaki üretici alanına göre yalnızca kendi ürünleri alınıyor.
    /// </summary>
    private static readonly string[] OwnManufacturers = ["Bigjoy", "Bigjoy Vitamins"];

    /// <summary>Tek istekte tüm kategoriyi almak için; en kalabalık kategori 44 ürün.</summary>
    private const int PageSize = 200;

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // Bir ürün birden fazla kategoride görünebiliyor; adrese göre tekil.
        var products = new Dictionary<string, ScrapedProduct>();

        foreach (var (categoryId, category) in Categories)
        {
            List<BigJoyProduct> results;
            try
            {
                results = await FetchCategoryAsync(categoryId, cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Tek bir kategori tüm taramayı düşürmemeli.
                continue;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);

            foreach (var item in results)
            {
                if (string.IsNullOrWhiteSpace(item.Href) || string.IsNullOrWhiteSpace(item.Name))
                    continue;

                if (!OwnManufacturers.Contains(item.Manufacturer, StringComparer.OrdinalIgnoreCase))
                    continue;

                var name = HttpUtility.HtmlDecode(item.Name).Trim();
                if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
                    continue;

                var url = BaseUrl + item.Href;
                if (products.ContainsKey(url))
                    continue;

                // "price" liste fiyatı; indirim varsa gerçek satış fiyatı
                // "special_price" alanında geliyor ve liste fiyatı mağazanın
                // beyan ettiği eski fiyat oluyor.
                decimal? current = ParsePrice(item.SpecialPrice) ?? ParsePrice(item.Price);
                if (current is null or <= 0)
                    continue;

                decimal? storeOld = ParsePrice(item.SpecialPrice) is not null ? ParsePrice(item.Price) : null;
                if (storeOld is not null && storeOld <= current)
                    storeOld = null;

                products[url] = new ScrapedProduct(
                    Name: name,
                    Url: url,
                    ImageUrl: string.IsNullOrWhiteSpace(item.Thumb) ? null : item.Thumb,
                    Category: category,
                    Price: current.Value,
                    StoreOldPrice: storeOld,
                    Description: CleanDescription(item.Description),
                    ServingsPerPackage: ParseServings(item.Gramaj));
            }
        }

        return products.Values.ToList();
    }

    private async Task<List<BigJoyProduct>> FetchCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["page"] = "1",
            ["limit"] = PageSize.ToString(),
            ["category_id"] = categoryId.ToString(),
            ["manufacturer_id"] = "",
            ["type"] = "0",
            ["search"] = "",
            ["sort"] = "",
        });

        using var response = await httpClient.PostAsync("api/product-category", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BigJoyCategoryResponse>(cancellationToken: cancellationToken);
        return payload?.Products ?? [];
    }

    /// <summary>"4.480,00 TL" → 4480.00. Alan indirim yokken false geldiği için nesne olarak okunuyor.</summary>
    private static decimal? ParsePrice(object? value)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text) || text.Equals("false", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return TurkishPriceParser.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gramaj alanı çoğunlukla ağırlık ("253g") ama bazen doğrudan servis
    /// sayısı ("21 Servis") — markanın kendi beyanı, türetilmiş değil.
    /// Ağırlık biçimindeyken null dönüyor; paket gramajı zaten ürün adından
    /// çıkarılıyor.
    /// </summary>
    private static int? ParseServings(string? gramaj)
    {
        if (string.IsNullOrWhiteSpace(gramaj))
            return null;

        var match = ServingsRegex().Match(gramaj);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) && count > 0
            ? count
            : null;
    }


    /// <summary>
    /// Besin değeri tablosu ve porsiyon bilgisi — yalnızca ürün DETAY
    /// sayfasında var, kategori ucunda yok.
    /// </summary>
    /// <remarks>
    /// <b>Neden sonradan eklendi.</b> 5 Eylül'de ölçüldü: inceleme sayfası
    /// olan 663 üründen 335'inde açıklama vardı ama besin değeri yoktu ve
    /// bunların 105'i BigJoy'du. Kaynakta veri EKSİK DEĞİL — sayfa Enerji,
    /// Yağ, Karbonhidrat, Protein satırlarını ve "Porsiyon Büyüklüğü / Sayısı"
    /// bilgisini eksiksiz yayınlıyor; sadece <c>&lt;table&gt;</c> yerine
    /// <c>div.bdegersatir</c> satırları kullanıyor.
    ///
    /// <b>Açıklama BİLİNÇLİ olarak null dönüyor.</b> BigJoy'un açıklaması
    /// zaten normal taramada (kategori ucunda) geliyor ve daha temiz; burada
    /// tekrar okumak aynı veriyi ikinci bir biçimde üretme riski taşırdı.
    /// Detay tamamlama servisi <c>??=</c> kullandığı için null hiçbir şeyi
    /// silmiyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromRowElements(
                doc.DocumentNode,
                "//div[contains(@class,'bdegersatir')]"));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: ReadServingSizeGrams(doc),
            ServingsPerPackage: ReadServingsPerPackage(doc));
    }

    /// <summary>"Porsiyon Büyüklüğü: 32g" satırından gramajı okur.</summary>
    private static decimal? ReadServingSizeGrams(HtmlDocument doc)
    {
        var text = ReadNutritionTitle(doc, "Porsiyon Büyüklüğü");
        if (text is null)
            return null;

        return NutritionServingParser.Grams(text);
    }

    /// <summary>"Porsiyon Sayısı: 68" satırından servis adedini okur.</summary>
    private static int? ReadServingsPerPackage(HtmlDocument doc)
    {
        var text = ReadNutritionTitle(doc, "Porsiyon Sayısı");
        if (text is null)
            return null;

        return NutritionServingParser.Count(text);
    }

    /// <summary>
    /// Porsiyon bilgisi <c>div.nutrition-title</c> içinde "etiket: değer"
    /// olarak duruyor. Etiket Türkçe karakterli olduğu için karşılaştırma
    /// KÜLTÜRE BIRAKILMIYOR: aranan metin sayfada birebir geçtiği gibi
    /// yazılıyor ve ordinal karşılaştırılıyor — <c>IgnoreCase</c> Türkçe
    /// noktalı İ'yi katlamıyor, "PORSİYON" ile "Porsiyon" eşleşmezdi.
    /// </summary>
    private static string? ReadNutritionTitle(HtmlDocument doc, string label)
    {
        var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'nutrition-title')]");
        if (nodes is null)
            return null;

        foreach (var node in nodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText) ?? string.Empty;
            if (text.Contains(label, StringComparison.Ordinal))
                return text;
        }

        return null;
    }

    private static string? CleanDescription(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var text = HttpUtility.HtmlDecode(html);
        text = TagRegex().Replace(text, " ");
        // Yanıtın sonunda OpenCart'ın kırpma işareti olarak ".." bırakıyor.
        text = text.TrimEnd().TrimEnd('.').Trim();
        text = string.Join(
            "\n\n",
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0));

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex(@"(\d+)\s*servis", RegexOptions.IgnoreCase)]
    private static partial Regex ServingsRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

}

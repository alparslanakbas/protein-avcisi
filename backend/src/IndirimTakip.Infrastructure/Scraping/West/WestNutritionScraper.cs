using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.NutritionLabels;

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
public partial class WestNutritionScraper(HttpClient httpClient, INutritionLabelOcr labelOcr) : IBrandScraper, IProductDetailFetcher
{
    private const string SiteUrl = "https://www.westnutrition.com.tr";

    public string BrandName => "West Nutrition";
    public string BaseUrl => SiteUrl;

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

                // Sitede 0 TL ile listelenen ürünler var (fiyatı girilmemiş
                // kayıtlar). Bunları almak hem anlamsız bir fiyat geçmişi
                // üretir hem de indirim oranı hesabında sıfıra bölmeye yol
                // açar — fiyatı olmayan ürünü hiç almıyoruz.
                if (price <= 0)
                    continue;

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

    /// <summary>
    /// Besin değeri ürün galerisindeki ETİKET GÖRSELİNDEN, Türkçe OCR ile.
    /// </summary>
    /// <remarks>
    /// <b>ÖLÇÜM (15 Eylül, 173 ürün).</b> West besin tablosunu metin olarak
    /// yayınlamıyor; 66 üründe galeride dosya adı "enerji-besin-ogeleri"
    /// benzeri bir görsel var. Canlı konteynerde okunan 70 görselde 14 kabul:
    /// iki sütunlu (100 g | Porsiyonda) whey etiketleri. Amino/kreatin/BCAA/
    /// karnitin etiketlerinin çoğu TEK sütun basıyor; satır kontrolü
    /// yapılamadığı için bilerek reddediliyor.
    ///
    /// <b>YALNIZCA ÜRÜNÜN KENDİ KLASÖRÜ.</b> Sayfa "benzer ürünler" bloğunda
    /// başka ürünlerin görsellerini de taşıyor ve onlar da
    /// <c>myassets/products/&lt;n&gt;/</c> altında. Ölçüldü: 892 numaralı karnitin
    /// sayfasında 083 (kendi) ve 488 (başka ürün) klasörlerinden iki etiket
    /// vardı. Klasör, ürünün ana görselinden (<c>itemprop="image"</c>) alınıyor;
    /// ana görsel yoksa etiket aranmıyor, tahmin edilmiyor.
    ///
    /// <b>Açıklama da döndürülüyor.</b> Normal tarama West'te açıklama
    /// getirmiyor (173/173 boş) ve tamamlama servisi açıklaması boş ürünü her
    /// turda yeniden seçiyor; dönmeseydi aynı görseller her turda OCR'lanırdı.
    ///
    /// <b>Tesseract yoksa hata atılıyor</b> — boş dönmek ürünü kalıcı olarak
    /// "tablo yok" diye damgalatırdı (Nois ile aynı gerekçe).
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        if (!labelOcr.IsAvailable)
            throw new InvalidOperationException("Tesseract (Türkçe dil paketiyle) kurulu değil; West etiketleri okunamıyor.");

        using var response = await httpClient.GetAsync(productUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ProductDetails(null, null, null);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var description = Description(doc);

        foreach (var imageUrl in LabelImageUrls(doc, html).Take(2))
        {
            using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken);
            if (!imageResponse.IsSuccessStatusCode)
                continue;

            var image = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            if (await NutritionLabelReader.ReadAsync(labelOcr, image, cancellationToken) is not { } label)
                continue;

            var nutritionJson = NutritionParser.BuildNutritionJson(label.Rows);
            return new ProductDetails(
                Description: description,
                NutritionJson: nutritionJson,
                ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
                ServingSizeGrams: label.ServingGrams);
        }

        return new ProductDetails(description, null, null);
    }

    /// <summary>Ürün sekmesindeki açıklama metni ("Ürün Bilgisi").</summary>
    internal static string? Description(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' product-detail-tab-content ')]" +
            "/div[contains(concat(' ', normalize-space(@class), ' '), ' product-detail-tab-row ')]");
        if (node is null)
            return null;

        var text = WhitespaceRegex().Replace(HtmlEntity.DeEntitize(node.InnerText), " ").Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Ürünün KENDİ görsel klasöründeki etiket görsellerinin tam boy adresleri.</summary>
    internal static IReadOnlyList<string> LabelImageUrls(HtmlDocument doc, string html)
    {
        var mainImage = doc.DocumentNode.SelectSingleNode("//img[@itemprop='image']")?.GetAttributeValue("src", "");
        if (string.IsNullOrEmpty(mainImage) || ProductFolderRegex().Match(mainImage) is not { Success: true } folderMatch)
            return [];

        var folder = folderMatch.Groups[1].Value;
        return ProductImageRegex().Matches(html)
            .Where(m => m.Groups[1].Value == folder && LabelFileRegex().IsMatch(m.Groups[2].Value))
            // "_min" küçük önizleme; tam boyu okunuyor.
            .Select(m => $"{SiteUrl}/myassets/products/{folder}/{MinSuffixRegex().Replace(WebUtility.HtmlDecode(m.Groups[2].Value), "$1")}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [GeneratedRegex(@"myassets/products/(\d+)/")]
    private static partial Regex ProductFolderRegex();

    [GeneratedRegex(@"myassets/products/(\d+)/([^""'?\s)]+)")]
    private static partial Regex ProductImageRegex();

    [GeneratedRegex(@"besin|enerji", RegexOptions.IgnoreCase)]
    private static partial Regex LabelFileRegex();

    [GeneratedRegex(@"_min(\.\w+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MinSuffixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

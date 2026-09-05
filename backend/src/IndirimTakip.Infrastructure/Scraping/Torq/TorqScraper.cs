using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Torq;

/// <summary>
/// Torq Nutrition (OpenCart). SSN ile aynı altyapı ama kategori kategori
/// gezmeye gerek yok: arama rotası boş sorguyla tüm katalogu tek listede
/// veriyor, bu yüzden hem daha az istek atıyoruz hem de kategori listesini
/// elle güncel tutma yükü doğmuyor.
/// </summary>
/// IProductDetailFetcher uzun süre BİLİNÇLİ olarak uygulanmamıştı: ürün
/// sayfasındaki AÇIKLAMA tablosu boş geliyor (içerik sonradan yükleniyor) ve
/// boş dönen bir çekici, detay tamamlama servisinin "kontrol edildi" damgasını
/// atıp ürünü sonsuza kadar dışlamasına yol açardı.
///
/// 5 Eylül'de yeniden ölçüldü ve gerekçe yalnızca AÇIKLAMA için geçerliymiş:
/// BESİN DEĞERİ sunucudan gelen HTML'de eksiksiz duruyor
/// (div.satirlar > span.baslik + span.deger), ayrıca porsiyon büyüklüğü ve
/// servis sayısı da div.ust_bilgiler içinde yazılı. Çekici artık var ama
/// yalnızca bunları dolduruyor; Description hâlâ null dönüyor — yani eski
/// gerekçe korunuyor, kapsamı daralıyor.
public class TorqScraper(HttpClient httpClient) : IBrandScraper, IProductDetailFetcher
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


    /// <summary>
    /// Besin değeri ve porsiyon bilgisi — yalnızca ürün DETAY sayfasında var.
    /// </summary>
    /// <remarks>
    /// Yapı Hardline ve BigJoy ile aynı aileden: besin satırları
    /// <c>div.satirlar</c>, porsiyon bilgisi <c>div.ust_bilgiler</c>, ikisinde
    /// de etiket <c>span.baslik</c> ve değer <c>span.deger</c>.
    ///
    /// <b>Açıklama BİLEREK çekilmiyor</b> (null dönüyor): sınıf yorumundaki
    /// eski ölçüm hâlâ geçerli, açıklama sunucu HTML'inde boş. Detay tamamlama
    /// servisi <c>??=</c> kullandığı için null hiçbir şeyi silmiyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromRowElements(doc.DocumentNode, "//div[contains(@class,'satirlar')]"));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: NutritionServingParser.Grams(UstBilgi(doc, "Porsiyon Büyüklüğü")),
            ServingsPerPackage: NutritionServingParser.Count(UstBilgi(doc, "Porsiyon Sayısı")));
    }

    /// <summary>
    /// <c>div.ust_bilgiler</c> satırlarından etiketi eşleşenin DEĞER kısmını
    /// döndürür.
    /// </summary>
    /// <remarks>
    /// Yalnızca <c>span.deger</c> okunuyor, satırın tamamı değil: "Porsiyon
    /// Büyüklüğü: 30 Gram" metninin tamamı verilseydi etiketteki bir sayı
    /// (ileride eklenebilecek "1. Porsiyon" gibi) değer sanılabilirdi.
    ///
    /// Karşılaştırma ORDINAL — <c>IgnoreCase</c> Türkçe noktalı İ'yi
    /// katlamıyor, aranan metin sayfada geçtiği gibi yazılıyor.
    /// </remarks>
    private static string? UstBilgi(HtmlDocument doc, string label)
    {
        var rows = doc.DocumentNode.SelectNodes("//div[contains(@class,'ust_bilgiler')]");
        if (rows is null)
            return null;

        foreach (var row in rows)
        {
            var baslik = row.SelectSingleNode(".//span[contains(@class,'baslik')]");
            if (baslik is null)
                continue;

            var text = HtmlEntity.DeEntitize(baslik.InnerText) ?? string.Empty;
            if (!text.Contains(label, StringComparison.Ordinal))
                continue;

            var deger = row.SelectSingleNode(".//span[contains(@class,'deger')]");
            return deger is null ? null : HtmlEntity.DeEntitize(deger.InnerText)?.Trim();
        }

        return null;
    }

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

using System.Net.Http.Json;
using System.Web;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.BigJoy;

/// <summary>
/// BigJoy — Nuxt tabanlı bir SPA, sayfa kaynağında fiyat yok; sitenin kendi
/// arka uç ucu kullanılıyor.
/// </summary>
/// <remarks>
/// <b>22 Eylül'de site yeniden yazıldı ve ESKİ UÇ KAYBOLDU.</b> Kullanılan
/// <c>POST /api/product-category</c> artık JSON değil SPA kabuğunu döndürüyor,
/// yani tarama dört turda da <c>'&lt;' is an invalid start of a value</c> ile
/// düştü ve 149 ürün 26 saat bayat kaldı (sağlık ucu yakaladı). Yeni uç
/// <c>GET /api/products?limit=..&amp;page=..</c>: katalogun tamamı tek istekte,
/// kategori kimlikleri ürünün İÇİNDE, yani artık kategori kategori gezmeye
/// gerek yok.
///
/// <b>Satır başına VARYANT.</b> Liste ürün GRUPLARI döndürüyor ama her aroma
/// ve gramajın kendi sayfası var (174 grup, 241 varyant sayfası; ölçüldü) ve
/// eski katalogumuzdaki 149 adresin 147'si bu kümede. Grup başına satır
/// üretmek o adresleri yetim bırakırdı.
///
/// <b>Fiyatlar KDV'siz geliyor.</b> Varyantta yalnızca <c>price</c>/<c>special</c>
/// var; sitede görünen fiyat ürünün <c>tax_rate</c> alanıyla hesaplanıyor.
/// Doğrulandı: hesap üç üründe de sayfadaki fiyatı birebir verdi (540, 2.250,
/// 1.200 TL) ve ürün düzeyindeki <c>price_with_tax</c> ile aynı çıktı.
/// </remarks>
public partial class BigJoyScraper(HttpClient httpClient) : IBrandScraper, IProductDetailFetcher
{
    public string BrandName => "BigJoy";
    public string BaseUrl => "https://www.bigjoy.com.tr";

    /// <summary>
    /// Kategori kimliği ve bizim slug'ımıza eşlemesi. Kimlikler ürünün kendi
    /// <c>category_ids</c> alanında geliyor. Anlamı belirsiz olanlar (Performans
    /// ve Güç, Endurance, Avantajlı Paketler) bilinçli olarak null: yanlış
    /// kategori, kategorisiz kalmaktan kötü — isimden çıkarıma bırakılıyor.
    /// </summary>
    private static readonly (int Id, string? Category)[] Categories =
    [
        // Sıra önemli: bir ürün birden fazla kategoride ve ilk eşleşen kazanıyor.
        // Mağaza gainer'ları hem "Kilo ve Hacim" hem "Protein Tozu" altında
        // listeliyor; dar kategoriler önce geliyor ki Mass Attack gibi ürünler
        // protein tozu sayılmasın.
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

    private const int PageSize = 200;

    /// <summary>Katalog 174 ürün; sayfa döngüsü sonsuza gitmesin diye tavan.</summary>
    private const int MaxPages = 10;

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // Aynı varyant birden çok grupta görünebiliyor; adrese göre tekil.
        var products = new Dictionary<string, ScrapedProduct>(StringComparer.OrdinalIgnoreCase);

        for (var page = 1; page <= MaxPages; page++)
        {
            BigJoyCategoryResponse? payload;
            try
            {
                payload = await FetchPageAsync(page, cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Tek bir sayfa tüm taramayı düşürmemeli; elde olan kaydedilir.
                break;
            }

            if (payload is null || payload.Products.Count == 0)
                break;

            foreach (var item in payload.Products)
                AddVariants(item, products);

            if (!payload.HasMore)
                break;

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        return products.Values.ToList();
    }

    private void AddVariants(BigJoyProduct item, Dictionary<string, ScrapedProduct> products)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            return;

        if (!OwnManufacturers.Contains(item.ManufacturerName, StringComparer.OrdinalIgnoreCase))
            return;

        var groupName = HttpUtility.HtmlDecode(item.Name).Trim();
        var category = CategoryFor(item.CategoryIds);
        var taxRate = item.TaxRate ?? 0m;

        foreach (var variant in VariantsOf(item))
        {
            if (string.IsNullOrWhiteSpace(variant.SeoKeyword))
                continue;

            var url = $"{BaseUrl}/{variant.SeoKeyword.Trim('/')}";
            if (products.ContainsKey(url))
                continue;

            // Ad: grup adı + gramaj ("Bigjoy Creatine Monohydrate" + "255g").
            // Aroma BİLEREK eklenmiyor: eski katalogda da yoktu (149 satır, 145
            // ad) ve ad değişse sitedeki ürün adresleri de değişirdi.
            var name = ComposeName(groupName, variant.SubgroupValue);
            if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
                continue;

            var listPrice = WithTax(variant.Price, taxRate);
            var specialPrice = WithTax(variant.Special, taxRate);

            var current = specialPrice ?? listPrice;
            if (current is null or <= 0)
                continue;

            var storeOld = specialPrice is not null ? listPrice : null;
            if (storeOld is not null && storeOld <= current)
                storeOld = null;

            products[url] = new ScrapedProduct(
                Name: name,
                Url: url,
                ImageUrl: ImageUrlOf(variant.Image ?? item.Thumb),
                Category: category,
                Price: current.Value,
                StoreOldPrice: storeOld,
                InStock: variant.IsInStock);
        }
    }

    /// <summary>
    /// Varyant listesi boşsa ürünün kendisi tek varyant sayılıyor: küçük bir
    /// azınlık ama onları düşürmek kataloğu eksiltirdi.
    /// </summary>
    private static IEnumerable<BigJoyVariant> VariantsOf(BigJoyProduct item) =>
        item.VariantAttributes.Count > 0
            ? item.VariantAttributes
            : [new BigJoyVariant
            {
                SeoKeyword = item.SeoKeyword,
                SubgroupValue = item.SubgroupValue,
                Price = item.Price,
                Special = item.Special,
                IsInStock = item.IsInStock,
            }];

    private static string ComposeName(string groupName, string? subgroup)
    {
        var gramaj = subgroup?.Trim();
        if (string.IsNullOrEmpty(gramaj) || groupName.Contains(gramaj, StringComparison.OrdinalIgnoreCase))
            return groupName;

        return $"{groupName} {gramaj}";
    }

    private static string? CategoryFor(List<int> categoryIds)
    {
        foreach (var (id, category) in Categories)
        {
            if (categoryIds.Contains(id))
                return category;
        }

        return null;
    }

    /// <summary>KDV'siz fiyattan sitede görünen fiyata.</summary>
    private static decimal? WithTax(decimal? price, decimal taxRatePercent) =>
        price is null or <= 0 ? null : Math.Round(price.Value * (1 + taxRatePercent / 100m), 2);

    private string? ImageUrlOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{BaseUrl}/{path.TrimStart('/')}";
    }

    private async Task<BigJoyCategoryResponse?> FetchPageAsync(int page, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/products?limit={PageSize}&page={page}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BigJoyCategoryResponse>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Besin değeri tablosu ve porsiyon bilgisi — yalnızca ürün DETAY
    /// sayfasında var, listeleme ucunda yok.
    /// </summary>
    /// <remarks>
    /// <b>Açıklama HÂLÂ null ve bu artık bir EKSİK.</b> Eski listeleme ucu
    /// açıklamayı veriyordu, bu yüzden burada bilerek boş dönülüyordu; yeni
    /// uçta o alan yok. Var olan açıklamalar duruyor (detay tamamlama
    /// <c>??=</c> kullanıyor) ama yeni ürünler açıklamasız kalıyor. Sayfadan
    /// okumak ayrı bir iş; buraya eklenirse aynı verinin iki biçimde
    /// üretilmediğinden emin olunmalı.
    ///
    /// <b>Seçiciler 22 Eylül'de değişti.</b> Eski <c>div.bdegersatir</c> ve
    /// <c>div.nutrition-title</c> artık sayfada YOK (ölçüldü: sıfır eşleşme);
    /// satırlar "Besin Değerleri" başlığının altında iki <c>span</c> olarak
    /// duruyor. Sınıf adları Tailwind üretimi ve kırılgan olduğu için çapa
    /// olarak BAŞLIK METNİ kullanılıyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromRowElements(
                doc.DocumentNode,
                // ancestor::div[2]: h3'ün EBEVEYNİ yalnızca başlık satırı, tablo
                // onun KARDEŞİNDE. [not(h3)] başlık satırını eliyor, yoksa
                // "Besin Değerleri | Her Porsiyon / 3.04g" diye bir satır girerdi.
                "//h3[contains(text(),'Besin Değerleri')]/ancestor::div[2]"
                    + "//div[contains(@class,'justify-between')][span][not(h3)]"));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: NutritionServingParser.Grams(ReadLabelled(doc, "Porsiyon Büyüklüğü")),
            ServingsPerPackage: NutritionServingParser.Count(ReadLabelled(doc, "Porsiyon Sayısı")));
    }

    /// <summary>
    /// "Porsiyon Büyüklüğü:" etiketinin yanındaki değeri okur.
    /// </summary>
    /// <remarks>
    /// Karşılaştırma KÜLTÜRE BIRAKILMIYOR: aranan metin sayfada geçtiği gibi
    /// yazılıyor ve ordinal karşılaştırılıyor — <c>IgnoreCase</c> Türkçe
    /// noktalı İ'yi katlamıyor, "PORSİYON" ile "Porsiyon" eşleşmezdi.
    /// </remarks>
    private static string? ReadLabelled(HtmlDocument doc, string label)
    {
        var spans = doc.DocumentNode.SelectNodes("//span");
        if (spans is null)
            return null;

        foreach (var span in spans)
        {
            var text = HtmlEntity.DeEntitize(span.InnerText)?.Trim() ?? string.Empty;
            if (!text.StartsWith(label, StringComparison.Ordinal))
                continue;

            var value = span.SelectSingleNode("following-sibling::span[1]");
            if (value is not null)
                return HtmlEntity.DeEntitize(value.InnerText)?.Trim();
        }

        return null;
    }
}

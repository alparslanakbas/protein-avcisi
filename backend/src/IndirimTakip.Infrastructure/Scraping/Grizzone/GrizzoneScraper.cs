using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Grizzone;

/// <summary>
/// grizzone.com.tr — otuzuncu kaynak. ikas; Gigi's/MLA ile aynı desen
/// (products.xml sitemap + ürün sayfasındaki schema.org bloğu).
///
/// <b>ÖLÇÜM (3 Eylül):</b> 116 adresten 114'ü veri verdi. Katalogda 35 ürün
/// takviye dışı — giyim (t-shirt, hoodie, atlet, eşofman), ekipman (strap,
/// kemer, wrist wrap, loop band), aksesuar (shaker, havlu, çanta, anahtarlık,
/// pillbox) ve çeşni (sıkılabilir sos, cajun baharatı). Kalan ~79 ürün gerçek
/// takviye: kreatin, BCAA, arginin, whey.
///
/// <b>Katalog TAMAMEN BÜYÜK HARF</b> ve bu, ortak süzgeçteki Türkçe harf
/// hatasını ortaya çıkardı: "ANAHTARLIK" gibi adlar `anahtarlık` kalıbıyla
/// eşleşmiyordu. Süzgeç artık adı ASCII'ye indirgeyip öyle eşleştiriyor.
/// </summary>
public sealed class GrizzoneScraper(HttpClient httpClient, ILogger<GrizzoneScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger), IProductDetailFetcher
{
    public override string BrandName => "Grizzone";
    public override string BaseUrl => "https://grizzone.com.tr";
    protected override string SitemapUrl => "https://grizzone.com.tr/products.xml";

    /// <summary>
    /// Besin değeri ve porsiyon bilgisi — <c>__NEXT_DATA__</c> içindeki ürün
    /// özelliklerinden.
    /// </summary>
    /// <remarks>
    /// <b>SAYFADA 83 AYRI BESİN TABLOSU VAR</b> ve bunların hemen hepsi menü
    /// yükünden geliyor; yalnızca <c>pageSpecificData</c> altındaki bu ürüne
    /// ait. JSON'da "Besin" geçen ilk tabloyu almak, BAŞKA bir ürünün
    /// değerlerini buraya yazmak olurdu — bu yüzden okuma
    /// <see cref="IkasProductAttributes"/> üzerinden yapılıyor.
    ///
    /// Tablo başlığı "Besin Öğeleri | 1 Porsiyon (5 g)" biçiminde, yani ilk
    /// satır BAŞLIK; <see cref="HtmlNutritionExtractor.FromMultiColumnTable"/>
    /// onu atlıyor ve yüzde sütunu varsa eliyor. Düz
    /// <c>FromTables</c> kullanılsaydı başlık satırı da veri sanılırdı
    /// ("Besin Öğeleri = 1 Porsiyon (5 g)" — içinde sayı olduğu için
    /// süzgeçten geçerdi).
    ///
    /// <b>Porsiyon kaynağın kendi alanlarından, uydurma yok:</b> "Ölçek /
    /// Gram" ve "Servis Adedi" ayrı TEXT alanları olarak geliyor. Bunlar
    /// yoksa porsiyon tablo başlığından okunuyor ("1 Porsiyon (5 g)").
    ///
    /// Açıklama çekilmiyor — normal taramada schema.org'dan geliyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await Http.GetStringAsync(productUrl, cancellationToken);
        var attributes = IkasProductAttributes.Read(html);

        var besinHtml = IkasProductAttributes.ValueOf(attributes, "besin");
        if (string.IsNullOrWhiteSpace(besinHtml))
            return new ProductDetails(null, null, null);

        var doc = new HtmlDocument();
        doc.LoadHtml(besinHtml);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromMultiColumnTable(doc.DocumentNode));

        var olcek = NutritionServingParser.Grams(IkasProductAttributes.ValueOf(attributes, "ölçek"))
            ?? NutritionServingParser.Grams(HtmlNutritionExtractor.MultiColumnPortionHeader(doc.DocumentNode));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: olcek,
            ServingsPerPackage: NutritionServingParser.Count(IkasProductAttributes.ValueOf(attributes, "servis adedi")));
    }
}

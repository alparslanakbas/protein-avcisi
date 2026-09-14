using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Kiperin;

/// <summary>
/// kiperinturkiye.com — otuz birinci kaynak. ikas; Gigi's/MLA/Grizzone ile
/// aynı desen.
///
/// <b>ÖLÇÜM (3 Eylül):</b> 48 adresin 48'i de veri verdi, hata yok.
/// Süzgeç HİÇBİR ürünü elemiyor — katalogun tamamı takviye: kolajen, B/D3K2
/// vitaminleri, B12 sprey, multivitamin. Vitamin ağırlıklı bir marka
/// (Vitabear ve GNC ile aynı kategori).
/// </summary>
public sealed class KiperinScraper(HttpClient httpClient, ILogger<KiperinScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger), IProductDetailFetcher
{
    public override string BrandName => "Kiperin";
    public override string BaseUrl => "https://kiperinturkiye.com";
    protected override string SitemapUrl => "https://kiperinturkiye.com/products.xml";

    /// <summary>
    /// Porsiyon başına besin/etken madde tablosu — ürünün KENDİ açıklamasından.
    /// </summary>
    /// <remarks>
    /// <b>ÖLÇÜM (15 Eylül, 58 ürün):</b> tablo ikas'ın özellik alanında DEĞİL,
    /// <c>pageSpecificData.description</c> içinde. Kapsül/vitaminlerde
    /// <c>Etken Madde | Miktar | %BRD</c> ("Magnezyum | 250 mg"), kolajen
    /// tozlarında <c>Bileşen | Miktar</c> içinde Enerji/Protein/Karbonhidrat/Yağ
    /// (Classic Collagen: 36 kcal = 4 × 9 g protein, tutuyor). GNC ile aynı
    /// karar: vitamin ürününde besin değerinin karşılığı etken madde tablosu.
    ///
    /// <b>Sütun:</b> <see cref="HtmlNutritionExtractor.FromMultiColumnTable"/>
    /// başlığında "%" geçmeyen en sağdaki sütunu alıyor, yani "%BRD" değil
    /// "Miktar" okunuyor.
    ///
    /// <b>Porsiyon gramı:</b> başlık "Miktar" diyor, gram vermiyor; alan boş
    /// kalıyor, kapsül sayısından uydurulmuyor.
    ///
    /// Açıklama metni döndürülmüyor — normal taramada zaten geliyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await Http.GetStringAsync(productUrl, cancellationToken);
        var aciklama = IkasProductAttributes.Description(html);

        if (string.IsNullOrWhiteSpace(aciklama) || !aciklama.Contains("<table", StringComparison.OrdinalIgnoreCase))
            return new ProductDetails(null, null, null);

        var doc = new HtmlDocument();
        doc.LoadHtml(aciklama);

        // "Kapsül Sayısı | 30" gibi satırlar kutudaki adet, besin ya da etken
        // madde değil. Canlı 58 sayfada 10'dan fazla tablonun son satırıydı;
        // besin tablosunda "Kapsül Sayısı: 30" görünmesi yanıltıcı olurdu.
        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromMultiColumnTable(doc.DocumentNode)
                .Where(satir => !AdetSatiri(satir.Label)));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: nutritionJson is null
                ? null
                : NutritionServingParser.Grams(HtmlNutritionExtractor.MultiColumnPortionHeader(doc.DocumentNode)),
            ServingsPerPackage: null);
    }

    // "Kapsül Sayısı", "Softgel Sayısı", "Tablet Sayısı" — Türkçe harfler elle
    // katlanıyor (bkz. turkce-metin-tuzaklari: invariant küçültme İ'yi çevirmez).
    private static bool AdetSatiri(string etiket)
    {
        var katli = etiket.Replace('İ', 'i').Replace('I', 'ı').ToLowerInvariant();
        return katli.Contains("sayısı", StringComparison.Ordinal)
            && (katli.Contains("kapsül", StringComparison.Ordinal)
                || katli.Contains("softgel", StringComparison.Ordinal)
                || katli.Contains("tablet", StringComparison.Ordinal));
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Kiperin;

/// <summary>
/// kiperin.com — otuz birinci kaynak. Shopify; Bahs/Fellas ile aynı desen.
///
/// <b>ikas'tan Shopify'a geçti (5 Kasım 2026).</b> Eski adres kiperinturkiye.com artık 301 ile
/// kiperin.com'a gidiyor, ikas'ın <c>products.xml</c>'i 404: 5 Kasım 10:12 UTC'den sonra her tur
/// boş döndü, sağlık ucu 32 saatte kırmızıya düştü. Shopify mağazası olduğu için öbür yedi Shopify
/// kaynağı gibi: <c>products.json</c>, bot kimliği, robots.txt, imza, 429'da Retry-After
/// (bkz. <see cref="ShopifyKatalog"/>). Ürün adresleri <c>/products/&lt;handle&gt;</c>; eski ikas
/// adresleri geçişte veritabanında bu biçime çevrildi ki fiyat geçmişi kopmasın.
///
/// <b>ÖLÇÜM (3 Eylül, ikas):</b> 48 adresin 48'i de veri verdi, hata yok.
/// Süzgeç HİÇBİR ürünü elemiyor — katalogun tamamı takviye: kolajen, B/D3K2
/// vitaminleri, B12 sprey, multivitamin. Vitamin ağırlıklı bir marka
/// (Vitabear ve GNC ile aynı kategori).
/// </summary>
public sealed class KiperinScraper(HttpClient httpClient, ILogger<KiperinScraper>? logger = null)
    : IBrandScraper, IProductDetailFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "Kiperin";
    public string BaseUrl => "https://kiperin.com";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ScrapedProduct>();

        for (var page = 1; ; page++)
        {
            var response = await ShopifyKatalog.SayfaGetirAsync(httpClient, page, JsonOptions, BrandName, logger, cancellationToken);

            if (response is null || response.Products.Count == 0)
                break;

            foreach (var product in response.Products)
            {
                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Title))
                    continue;

                var variant = product.Variants.Find(v => v.Available) ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Price <= 0)
                    continue;

                result.Add(new ScrapedProduct(
                    Name: product.Title,
                    Url: $"{BaseUrl}/products/{product.Handle}",
                    ImageUrl: product.Images.Count > 0 ? product.Images[0].Src : null,
                    Category: null,
                    Price: variant.Price,
                    StoreOldPrice: variant.CompareAtPrice > variant.Price ? variant.CompareAtPrice : null,
                    InStock: product.Variants.Any(v => v.Available)));
            }

            if (response.Products.Count < 250)
                break;
        }

        return result;
    }

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
    ///
    /// <b>Shopify'a geçince (5 Kasım):</b> mevcut ürünlerin besin tablosu ikas döneminden duruyor
    /// (detay bir kez dolduruluyor). Shopify sayfasında ikas açıklaması bulunmadığı için yeni bir
    /// üründe tablo boş kalır, uydurulmaz; Shopify açıklamasından okumak ayrı bir iş.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
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

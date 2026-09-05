using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Fellas;

/// <summary>
/// fellasfoods.com.tr — otuz üçüncü kaynak. Shopify; HIQ/Commander/Supra ile
/// aynı public <c>products.json</c> ucu, tek istekte katalog.
///
/// <b>AKSESUAR SÜZGECİ KAYNAĞIN KENDİ KATEGORİSİNDEN.</b> Bu mağazada
/// <c>product_type</c> DOLU ve güvenilir: "Aksesuar" 5, "Yüksek Protein Bar"
/// 25, "Granola" 18, "Meyve Bar" 12, "Protein Tozu" 11, "Nohut Cipsi" 9...
/// İsim tabanlı ortak süzgeç yerine kaynağın kendi etiketi kullanılıyor —
/// Nois'te verilen kararın aynısı. Ortak süzgeç yine de ÜSTÜNE çalışıyor
/// (kategori etiketi yanlışsa ad yakalasın diye).
///
/// <b>NİŞ NOTU.</b> Fellas bir ATIŞTIRMALIK markası: protein bar, granola,
/// meyve bar, nohut cipsi, fıstık ezmesi. Protein tozu da var (11 ürün).
/// Gigi's ile aynı kategoride; <c>saglikli-atistirmaliklar</c> kategorimize
/// oturuyor.
/// </summary>
public sealed partial class FellasScraper(HttpClient httpClient) : IBrandScraper, IProductDetailFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Kaynağın takviye/gıda DIŞI saydığı kategoriler. Şu an tek değer var
    /// ama liste olarak tutuluyor: mağaza yeni bir aksesuar kategorisi
    /// eklerse buraya bir satır yetsin.
    /// </summary>
    private static readonly HashSet<string> AksesuarKategorileri =
        new(StringComparer.OrdinalIgnoreCase) { "Aksesuar" };

    public string BrandName => "Fellas";
    public string BaseUrl => "https://fellasfoods.com.tr";

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
                if (product.ProductType is not null && AksesuarKategorileri.Contains(product.ProductType))
                    continue;
                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Title))
                    continue;

                // Stokta olan varyant varsa o, yoksa ilki: stokta olmayan ürün
                // taramadan DÜŞMEMELİ, yoksa fiyat geçmişinde boşluk oluşur.
                var variant = product.Variants.Find(v => v.Available) ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Price <= 0)
                    continue;

                result.Add(new ScrapedProduct(
                    Name: product.Title,
                    Url: $"{BaseUrl}/products/{product.Handle}",
                    ImageUrl: product.Images.Count > 0 ? product.Images[0].Src : null,
                    // Kaynağın kategorileri bizim slug'larımız değil
                    // ("Yüksek Protein Bar"); kategori isimden çıkarılıyor.
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
    /// Besin değeri ve porsiyon bilgisi — yalnızca ürün SAYFASINDA var.
    /// </summary>
    /// <remarks>
    /// <b>Neden ayrı istek gerekiyor.</b> Shopify markalarında besin tablosu
    /// genelde <c>body_html</c> içinde gelir (HIQ böyle) ve ek istek
    /// gerekmez. Fellas'ta ÖLÇÜLDÜ: kataloğun tamamı (123 ürün) çekildi ve
    /// <c>body_html</c>'lerin HİÇBİRİNDE besin tablosu yok — blok temanın
    /// kendi alanından geliyor ve sadece ürün sayfasında basılıyor.
    ///
    /// Yapı diğerleriyle aynı aileden: <c>div.nutrition-row</c> içinde iki
    /// <c>span</c> (etiket, değer).
    ///
    /// <b>Kapsam beklentisi ~%67</b> (12 üründe ölçüldü: 8 dolu). Boş
    /// dönenler çoklu paketler ("karma kutu", "deneme paketi") ve shaker —
    /// yani gerçekten tek bir besin tablosu OLMAYAN ürünler. Bu yüzden boş
    /// sonuç bir hata değil; detay tamamlama servisi damgayı atıp geçiyor.
    ///
    /// Açıklama BİLEREK çekilmiyor: normal taramada <c>body_html</c>'den
    /// zaten geliyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromRowElements(doc.DocumentNode, "//div[contains(@class,'nutrition-row')]"));

        var bilgi = doc.DocumentNode
            .SelectSingleNode("//*[contains(@class,'nutrition-info')]")?.InnerText;
        bilgi = bilgi is null ? null : HtmlEntity.DeEntitize(bilgi);

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: NutritionServingParser.Grams(bilgi),
            ServingsPerPackage: PorsiyonSayisi(bilgi));
    }

    /// <summary>
    /// "1 paket (300 g) yaklaşık 10 porsiyondur" cümlesinden porsiyon
    /// sayısını okur.
    /// </summary>
    /// <remarks>
    /// Ortak <see cref="NutritionServingParser.Count"/> BURADA KULLANILAMAZ:
    /// o, metindeki İLK sayıyı alıyor ve bu cümleler "Değerler 1 porsiyon…"
    /// diye başladığı için sonuç her üründe 1 çıkardı — sessizce yanlış bir
    /// servis sayısı, servis başı fiyatı paketin tamamına eşitlerdi.
    /// Bu yüzden "yaklaşık N porsiyon" kalıbı açıkça aranıyor; cümlede yoksa
    /// (bar gibi tek porsiyonluk ürünlerde yok) null kalıyor.
    /// </remarks>
    private static int? PorsiyonSayisi(string? bilgi)
    {
        if (string.IsNullOrWhiteSpace(bilgi))
            return null;

        var match = PorsiyonSayisiRegex().Match(bilgi);
        return match.Success && int.TryParse(match.Groups[1].Value, out var adet) && adet is > 0 and <= 1000
            ? adet
            : null;
    }

    // Harf sınıfları AÇIK yazıldı, IgnoreCase'e bırakılmadı: .NET'in
    // büyük/küçük harf katlaması Türkçe i/I çiftini bilmiyor ve sayfa
    // "YAKLAŞIK" diye yazsaydı 'I' -> 'i' olup desendeki 'ı' ile
    // eşleşmezdi (bu depoda aynı hata daha önce kategori ve marka
    // eşleştirmesini bozdu, bkz. 15897c0 ve 15eb010).
    [GeneratedRegex(@"yakla[şŞ][ıIiİ]k\s*(\d+)\s*porsiyon", RegexOptions.IgnoreCase)]
    private static partial Regex PorsiyonSayisiRegex();

}

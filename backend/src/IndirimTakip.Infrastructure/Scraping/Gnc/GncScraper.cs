using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Scraping.Gnc;

// GNC Türkiye — on ikinci kaynak, ikas altyapısı.
//
// Site dışarıdan Next.js görünüyor (robots.txt'te `/_next/data/*`) ama
// görseller cdn.myikas.com'dan geliyor: arkasında ikas var. Yani ProteinOcean
// ve Yeşilmarka için yazdığımız storefront GraphQL deseni burada da geçerli —
// bu scraper'ın sorgusu ProteinOcean'ınkiyle aynı, modelleri de paylaşıyor
// (`Scraping/ProteinOcean/IkasModels.cs`).
//
// ADAY ELEME (1 Eylül 2026, `scraper-ve-veri.md`'deki dört ölçüt):
//   1. Kendi sitesinden fiyatla satıyor mu?  EVET (API gerçek fiyat veriyor)
//   2. VM'den erişilebiliyor mu?             EVET (HTTP 200; eprotein 403 idi)
//   3. Altyapı tanıdık mı?                   EVET (ikas)
//   4. Ürün sayısı anlamlı mı?               51 (Commander 40 ile eklenmişti)
//
// KAPSAM: GNC ağırlıklı bir vitamin/sağlık markası — 51 ürünün yaklaşık 19'u
// spor takviyesi, kalanı vitamin/bitkisel. Kullanıcı hepsinin alınmasını
// istedi (1 Eylül): sitede "Vitamin & Mineral" kategorisi zaten var ve
// Aksu Vital / Nature's Bounty gibi vitamin markaları duruyor.
//
// SATICI: GNC kendi sitesinden satıyor, yani `Seller` null kalıyor
// (bayi değil). Marka sayfası bu ürünlerin kendi vitrini olur.
public partial class GncScraper(HttpClient httpClient) : IBrandScraper, IProductDetailFetcher
{
    public string BrandName => "GNC";
    public string BaseUrl => "https://gnc.com.tr";

    private const string MerchantId = "644fff85-4a8e-433c-af65-c036f85a6c03";
    private const string SalesChannelId = "f00492c0-de73-4fb6-90ae-028c6cb7dca4";
    private const string StorefrontId = "f226018d-c7d1-45f8-8b57-92bb5f0b46c7";

    // Sitenin kendi frontend'inin gönderdiği public storefront anahtarı — JWT
    // içinde yalnızca merchant/storefront/salesChannel ID'leri var, gizli bir
    // kimlik bilgisi değil (ProteinOcean'da da aynısı yapılıyor).
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtIjoiNjQ0ZmZmODUtNGE4ZS00MzNjLWFmNjUtYzAzNmY4NWE2YzAzIiwic2YiOiJmMjI2MDE4ZC1jN2QxLTQ1ZjgtOGI1Ny05MmJiNWYwYjQ2YzciLCJzZnQiOjEsInNsIjoiZjAwNDkyYzAtZGU3My00ZmI2LTkwYWUtMDI4YzZjYjdkY2E0In0.Z6duH-u34ssufJLQMGiTX4fR1LazLOJIJe4fvjhn18E";

    private const int PageSize = 100; // İkas sunucusu perPage'i 100 ile sınırlıyor.

    // ProteinOcean'ın sorgusunun aynısı. Variant `attributes` alanı BİLİNÇLİ
    // olarak istenmiyor: GNC'de boş geliyor (1 Eylül'de ölçüldü), servis
    // sayısı ürün adından çıkarılıyor.
    private const string SearchProductsQuery = """
        query searchProducts($input: SearchInput!) {
          searchProducts(input: $input) {
            totalCount
            results {
              name
              metaData { slug }
              variants {
                prices { sellPrice }
                images { id fileName isMain }
                stocks { stockCount }
              }
            }
          }
        }
        """;


    /// <summary>
    /// Porsiyon başına etken madde tablosu — <c>__NEXT_DATA__</c> içindeki
    /// "İçindekiler" alanından.
    /// </summary>
    /// <remarks>
    /// <b>ALAN ADI "İÇİNDEKİLER" AMA İÇERİĞİ TABLO.</b> GNC'nin kataloğu
    /// ağırlıkla vitamin/kapsül ve orada "besin değeri"nin karşılığı, porsiyon
    /// başına etken madde miktarı: <c>Etken Madde | 1 Yumuşak Kapsüldeki
    /// Miktar</c> → <c>Koenzim Q10 | 100 mg</c>. Kaynağın kendi beyanı,
    /// makine okunur ve porsiyon başına — sakladığımız şeyin tanımına uyuyor.
    ///
    /// <b>Aynı adlı alan başka kaynakta DÜZ METİN olabiliyor</b>
    /// (ProteinOcean'da içindekiler listesi). Ayrım koda gömülmedi: tablo
    /// yoksa <c>BuildNutritionJson</c> zaten null döndürüyor, yani metin
    /// kendiliğinden eleniyor.
    ///
    /// <b>Porsiyon gramajı YAZILMIYOR:</b> başlık "1 Yumuşak Kapsüldeki
    /// Miktar" diyor, gram vermiyor. Kapsül sayısından gram uydurmak yasak,
    /// alan boş kalıyor.
    ///
    /// Sayfadaki diğer ürünlerin verisi okunmuyor (bkz.
    /// <see cref="IkasProductAttributes"/>). Açıklama da çekilmiyor — normal
    /// taramada zaten geliyor.
    /// </remarks>
    public async Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(productUrl, cancellationToken);
        var attributes = IkasProductAttributes.Read(html);

        // Önce "besin" adlı bir alan varsa o (protein tozu gibi ürünlerde),
        // yoksa etken madde tablosu.
        var tabloHtml = IkasProductAttributes.ValueOf(attributes, "besin")
            ?? IkasProductAttributes.ValueOf(attributes, "içindekiler");

        if (string.IsNullOrWhiteSpace(tabloHtml))
            return new ProductDetails(null, null, null);

        var doc = new HtmlDocument();
        doc.LoadHtml(tabloHtml);

        var nutritionJson = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromMultiColumnTable(doc.DocumentNode));

        return new ProductDetails(
            Description: null,
            NutritionJson: nutritionJson,
            ProteinPerServingGrams: NutritionParser.ExtractProteinGrams(nutritionJson),
            ServingSizeGrams: nutritionJson is null
                ? null
                : NutritionServingParser.Grams(HtmlNutritionExtractor.MultiColumnPortionHeader(doc.DocumentNode)),
            ServingsPerPackage: null);
    }

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var products = new List<ScrapedProduct>();
        var receivedCount = 0;
        var page = 1;

        while (true)
        {
            var response = await SearchProductsPageAsync(page, cancellationToken);
            var result = response?.Data?.SearchProducts;
            var results = result?.Results ?? [];
            if (results.Count == 0)
                break;

            foreach (var product in results)
            {
                if (string.IsNullOrEmpty(product.MetaData?.Slug))
                    continue;

                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Name))
                    continue;

                var variant = product.Variants.Find(v => v.Stocks.Sum(s => s.StockCount) > 0)
                    ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Prices.Count == 0)
                    continue;

                // Stok bilgisi kaynakta var, o yüzden kaydediliyor: ürün stokta
                // değilken de taranmaya devam ediyor (fiyat geçmişi kesintisiz
                // kalsın diye), arayüzde "Tükendi" rozetiyle gösteriliyor.
                var inStock = product.Variants.Any(v => v.Stocks.Sum(s => s.StockCount) > 0);

                var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

                products.Add(new ScrapedProduct(
                    Name: product.Name,
                    Url: $"{BaseUrl}/{product.MetaData.Slug}",
                    ImageUrl: image is null
                        ? null
                        : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
                    Category: null,
                    Price: variant.Prices[0].SellPrice,
                    ServingsPerPackage: ExtractServingsPerPackage(product.Name),
                    InStock: inStock));
            }

            receivedCount += results.Count;
            if (receivedCount >= (result?.TotalCount ?? 0))
                break;

            page++;
        }

        return products;
    }

    /// <summary>
    /// Paketten kaç servis çıktığı. GNC bunu ürün ADINDA beyan ediyor:
    /// "Creatine MonoHydrate – 510 g (100 servis)".
    ///
    /// ProteinOcean'da bu bilgi variant'ın "Servis" özelliğinden geliyordu;
    /// GNC'de variant özellikleri BOŞ geliyor (1 Eylül'de API'ye bakılarak
    /// doğrulandı), tek kaynak ürün adı. Markanın kendi beyanı olduğu için
    /// alınıyor — türetilmiş ya da varsayılmış bir sayı değil. Adında
    /// geçmeyen üründe null kalıyor, tahmin üretilmiyor.
    /// </summary>
    internal static int? ExtractServingsPerPackage(string name)
    {
        var match = ServingsRegex().Match(name);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups["value"].ValueSpan, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var servings))
            return null;

        // Makul olmayan değerleri ele (ProteinOcean'daki sınırın aynısı).
        return servings is > 0 and <= 1000 ? servings : null;
    }

    // "(100 servis)" / "100 Servis" / "(15 servis)". Türkçe'de "servis"
    // sözcüğü ek almıyor bu kalıpta, ama büyük harfli yazım için IgnoreCase
    // şart — ve `\b` yerine açık sınır kullanmaya gerek yok çünkü aranan
    // sözcük tamamen ASCII.
    [GeneratedRegex(@"(?<value>\d{1,4})\s*servis", RegexOptions.IgnoreCase)]
    private static partial Regex ServingsRegex();

    private async Task<SearchProductsGraphQlResponse?> SearchProductsPageAsync(int page, CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = SearchProductsQuery,
            variables = new
            {
                input = new
                {
                    locale = "tr",
                    page,
                    perPage = PageSize,
                    filterList = Array.Empty<object>(),
                    facetList = Array.Empty<object>(),
                    salesChannelId = SalesChannelId,
                    query = "",
                    order = new[] { new { direction = "ASC", type = "MANUAL_SORT" } },
                    showStockOption = "SHOW_ALL",
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/sf/graphql?op=searchProducts")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("x-api-key", ApiKey);
        request.Headers.Add("x-sfid", StorefrontId);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchProductsGraphQlResponse>(cancellationToken: cancellationToken);
    }
}

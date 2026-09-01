using System.Net.Http.Json;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Scraping.SwissNutrition;

/// <summary>
/// Swiss Nutrition — İkas altyapısı, ProteinOcean/Yeşilmarka ile aynı public
/// storefront GraphQL API'si (api.myikas.com). Kimlikler sitenin kendi
/// sayfasındaki JWT'den alındı; her ziyaretçinin tarayıcısı da bunları
/// gönderiyor, gizli bir kimlik bilgisi değil.
///
/// İKİ ÖNEMLİ ÖZELLİK:
///
/// 1. <b>Karma katalog.</b> 178 ürünün yalnızca 91'i spor takviyesi. Geri
///    kalanı granola/pirinç patlağı (OrganikSatınAl), sos ve tatlandırıcı
///    (Dr. Pan), shaker/havlu/şapka ve çok ürünlü "avantaj paketleri".
///    Süzgeç olmadan siteye yulaf ezmesi girerdi. Yeşilmarka'daki ile aynı
///    yaklaşım: sitenin KENDİ kategori adlarına göre süzülüyor — isim
///    tahminine göre değil.
///
/// 2. <b>Çok markalı.</b> Katalogda Swiss Nutrition'ın kendi ürünlerinin
///    yanında başka üreticiler de var (Purevits, Herbina, FitNut, BioBee).
///    Bu yüzden <see cref="ScrapedProduct.Seller"/> ürün başına
///    belirleniyor: markanın kendi ürünü null (kendi sitesinden alınıyor),
///    başkasının ürünü ise "swissnutrition.com" (Swiss burada BAYİ).
/// </summary>
public class SwissNutritionScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Swiss Nutrition";
    public string BaseUrl => "https://swissnutrition.com";

    private const string MerchantId = "1b459dc8-71d6-4f18-9f64-f11ef472b7a0";
    private const string SalesChannelId = "6cf7b005-4ec4-4d40-9573-23260bba8a99";
    private const string StorefrontId = "32c6e499-9e17-4a60-a3c4-1f46f6b69415";

    /// <summary>
    /// Storefront API anahtarı (JWT). İçinde yalnızca merchant/storefront/
    /// satış kanalı kimlikleri var — yukarıdaki üç sabit zaten bu token'ın
    /// çözülmesiyle elde edildi.
    /// </summary>
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJtIjoiMWI0NTlkYzgtNzFkNi00ZjE4LTlmNjQtZjExZWY0NzJiN2EwIiwic2YiOiIzMmM2ZTQ5OS05ZTE3" +
        "LTRhNjAtYTNjNC0xZjQ2ZjZiNjk0MTUiLCJzZnQiOjEsInNsIjoiNmNmN2IwMDUtNGVjNC00ZDQwLTk1NzMt" +
        "MjMyNjBiYmE4YTk5In0.O3YowueSpKCFUw95ATQwwq7SSxpFiH_UfhsySiCg86A";

    private const int PageSize = 100; // İkas sunucusu perPage'i 100 ile sınırlıyor.

    private const string SellerName = "swissnutrition.com";

    /// <summary>
    /// Sitenin kendi kategori adlarından spor takviyesi sayılanlar. Ürün
    /// bunlardan EN AZ BİRİNE girmiyorsa alınmıyor.
    ///
    /// Kasıtlı olarak DIŞARIDA bırakılanlar ve sebepleri: "Gıda", "Ezmeler",
    /// "Tatlandırıcılar", "Baharat ve Soslar" (yiyecek, takviye değil),
    /// "Aksesuar" / "shaker" (ekipman), "Tüm Ürünler" (her şeyi içeriyor,
    /// süzgeç olarak işe yaramaz).
    /// </summary>
    private static readonly HashSet<string> SupplementCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Vitaminler",
        "Protein",
        "Whey Protein",
        "Amino Asit",
        "Kreatin",
        "Pre Workout & Intra Workout",
        "Protein Barlar",
        "Kilo ve Hacim",
        "L-KARNİTİN",
    };

    /// <summary>
    /// Birden çok ürünün bir arada satıldığı setler ("WHEY TANIŞMA PAKETİ",
    /// "FİTNESS PAKETİ 3"). Takviye kategorilerine de girdikleri için isimden
    /// değil bu kategoriden eleniyorlar.
    ///
    /// Neden eleniyor: tek bir fiyatı var ama içinde birden çok ürün var;
    /// servis başına maliyet, gramaj ve protein yoğunluğu gibi bizim
    /// ürettiğimiz ölçümlerin hiçbiri anlamlı çıkmıyor. Fiyat geçmişi
    /// tutmak da yanıltıcı: setin içeriği değişince fiyat "düşmüş" görünür.
    /// </summary>
    private const string BundleCategory = "Avantaj Paketleri";

    private const string SearchProductsQuery = """
        query searchProducts($input: SearchInput!) {
          searchProducts(input: $input) {
            totalCount
            results {
              name
              brand { name }
              metaData { slug }
              categories { name }
              variants {
                prices { sellPrice }
                images { id fileName isMain }
                stocks { stockCount }
              }
            }
          }
        }
        """;

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
                var scraped = Convert(product);
                if (scraped is not null)
                    products.Add(scraped);
            }

            receivedCount += results.Count;
            if (receivedCount >= (result?.TotalCount ?? 0))
                break;

            page++;
        }

        return products;
    }

    internal ScrapedProduct? Convert(IkasProduct product)
    {
        var slug = product.MetaData?.Slug;
        if (string.IsNullOrEmpty(slug))
            return null;

        var categories = product.Categories ?? [];
        if (!categories.Any(c => c.Name is not null && SupplementCategories.Contains(c.Name)))
            return null;

        if (categories.Any(c => string.Equals(c.Name, BundleCategory, StringComparison.OrdinalIgnoreCase)))
            return null;

        // Kategori süzgecini geçse bile ekipman adı taşıyan bir ürün olabilir
        // (ör. kategorisi yanlış girilmiş bir shaker) — ortak süzgeç ucuz.
        if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Name))
            return null;

        var brand = NormalizeBrand(product.Brand?.Name, product.Name);
        if (brand is null)
            return null;

        var variant = product.Variants.Find(v => v.Stocks.Sum(s => s.StockCount) > 0)
            ?? product.Variants.FirstOrDefault();
        if (variant is null || variant.Prices.Count == 0)
            return null;

        var price = variant.Prices[0].SellPrice;
        // Fiyatı girilmemiş ürün, indirim oranı hesabında sıfıra bölmeye ve
        // gerçekte olmayan bir "bedava ürün" fırsatına yol açmamalı.
        if (price <= 0)
            return null;

        // Stok bilgisi kaynakta var: ürün tükendiğinde taramadan DÜŞMÜYOR
        // (fiyat geçmişi kesintisiz kalsın diye), arayüzde "Tükendi"
        // rozetiyle gösteriliyor.
        var inStock = product.Variants.Any(v => v.Stocks.Sum(s => s.StockCount) > 0);

        var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

        return new ScrapedProduct(
            Name: product.Name,
            Url: $"{BaseUrl}/{slug}",
            ImageUrl: image is null
                ? null
                : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
            // Sitenin kategori adları ("Kilo ve Hacim") bizim slug'larımıza
            // birebir oturmuyor; isimden çıkarım diğer markalarla tutarlı
            // sonuç veriyor (bkz. ProductAttributeParser.InferCategory).
            Category: null,
            Price: price,
            BrandName: brand,
            InStock: inStock,
            // Markanın kendi ürünü kendi sitesinden alınıyor → satıcı yok.
            // Başkasının ürününde Swiss bayi konumunda.
            Seller: brand == BrandName ? null : SellerName);
    }

    /// <summary>
    /// Ürünün üretici markasını belirler. Kaynaktaki <c>brand</c> alanı her
    /// zaman gerçek üreticiyi göstermiyor.
    /// </summary>
    /// <returns>Marka adı, ya da güvenilir biçimde belirlenemiyorsa null —
    /// bu durumda ürün hiç alınmıyor, uydurma marka yaratmaktansa atlanıyor.</returns>
    internal static string? NormalizeBrand(string? raw, string productName)
    {
        var brand = raw?.Trim();
        if (string.IsNullOrEmpty(brand))
            return null;

        // Katalogda markanın kendi ürünleri iki farklı adla geçiyor.
        if (brand.Equals("Swiss", StringComparison.OrdinalIgnoreCase))
            return "Swiss Nutrition";

        // "OrganikSatınAl" bir ÜRETİCİ değil, kardeş bir satış sitesinin adı;
        // marka alanına yanlışlıkla o yazılmış. Bu etiketi taşıyan ürünlerin
        // hepsi (7/7) adı "FİTNUT" ile başlayan protein barlar, yani gerçek
        // üretici isimde duruyor. Başka bir ürüne bu etiket takılırsa üreticiyi
        // BİLMİYORUZ demektir — tahmin üretmek yerine ürün atlanıyor.
        if (brand.Equals("OrganikSatınAl", StringComparison.OrdinalIgnoreCase))
        {
            return productName.StartsWith("FİTNUT", StringComparison.OrdinalIgnoreCase)
                || productName.StartsWith("FITNUT", StringComparison.OrdinalIgnoreCase)
                    ? "FitNut"
                    : null;
        }

        return BrandNameNormalizer.Normalize(brand);
    }

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

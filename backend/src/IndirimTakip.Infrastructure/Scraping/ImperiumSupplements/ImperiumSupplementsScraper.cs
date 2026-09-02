using System.Net.Http.Json;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Scraping.ImperiumSupplements;

// Imperium Supplements — yirmi birinci kaynak. ikas altyapısı, GNC/Heyday ile
// aynı storefront GraphQL sorgusu; farkı yalnızca kimlikler ve aşağıdaki
// kategori beyaz listesi.
//
// KATALOG KARIŞIK: 43 ürünün yalnızca 28'i takviye. Kalanı spor çantası,
// havlu, shaker ve KURU GIDA (kurutulmuş kivi/portakal/elma, limon kurusu,
// bal). Kuru meyve bizim nişimiz değil ve ortak aksesuar süzgeci onu
// tanımıyor — ama kaynağın KENDİ kategorileri ayrımı net veriyor.
//
// BU YÜZDEN BEYAZ LİSTE: yalnızca "Sporcu Besinleri" ya da "Takviye Edici
// Gıdalar" kategorisindeki ürünler alınıyor. Kara liste (aksesuar/kuru gıda
// kategorilerini elemek) de denendi ve ölçüldü: 43 → 29 bırakıyor, ama
// "Domates Kurusu 50G" sızıyor çünkü o üründe hiç anlamlı kategori yok,
// sadece "Tüm Ürünler" var. Beyaz liste onu da eliyor ve daha dayanıklı:
// mağaza yarın yeni bir aksesuar kategorisi açarsa kendiliğinden dışarıda
// kalır. Bedeli, kategorisiz eklenen gerçek bir ürünü kaçırmak — kuru domates
// yayınlamaktansa bir ürünü kaçırmak yeğdir.
//
// MARKA TEK: kaynağın `brand` alanı yedi ayrı ad taşıyor (Hard Pump 23,
// Dry İmperium 6, Jove Complex 4, Şşt 2, İm-Fit...) ama bunlar bağımsız
// üreticiler değil, aynı şirketin ÜRÜN HATLARI — "Dry İmperium" adı bile
// mağazanın adını taşıyor. HIQ'nun "Smash Pro"su ya da BigJoy'un "BigWhey"i
// nasıl ayrı marka sayılmıyorsa bunlar da sayılmıyor; hepsi tek marka
// altında toplanıyor. Marka adı sitenin kendi `og:site_name` etiketinden.
//
// Çok ürünlü setler (katalogda ~10 tane) BİLEREK TUTULUYOR — sitenin
// yerleşik davranışı bu (bkz. CLAUDE.md, paket politikası).
public class ImperiumSupplementsScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Imperium Supplements";
    public string BaseUrl => "https://imperiumsup.com";

    private const string MerchantId = "6d417756-477e-4ab9-be12-f8f0cee73d4f";
    private const string SalesChannelId = "ab4d1b63-7a7e-49ab-98db-b08f3f18da4a";
    private const string StorefrontId = "3c58f130-aee6-432b-b8de-b7f97729a3f8";

    // Sitenin kendi frontend'inin gönderdiği public storefront anahtarı.
    private const string ApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtIjoiNmQ0MTc3NTYtNDc3ZS00YWI5LWJlMTItZjhmMGNlZTczZDRmIiwic2YiOiIzYzU4ZjEzMC1hZWU2LTQzMmItYjhkZS1iN2Y5NzcyOWEzZjgiLCJzZnQiOjEsInNsIjoiYWI0ZDFiNjMtN2E3ZS00OWFiLTk4ZGItYjA4ZjNmMThkYTRhIn0.wxxKroh46EQKOll8z5x8s04Z0DRTTW32DHyc1KmgevA";

    private const int PageSize = 100;

    /// <summary>
    /// Yalnızca bu kategorilerdeki ürünler alınıyor — gerekçe sınıf yorumunda.
    /// Kaynağın diğer kategorileri: "Sporcu Ekipmanları" (çanta/havlu/shaker),
    /// "Kurutulmuş Ürünler", "Doğal Gıdalar" (bal) ve içerik taşımayan
    /// "Tüm Ürünler".
    /// </summary>
    private static readonly string[] AllowedCategories =
    [
        "Sporcu Besinleri",
        "Takviye Edici Gıdalar",
    ];

    private const string SearchProductsQuery = """
        query searchProducts($input: SearchInput!) {
          searchProducts(input: $input) {
            totalCount
            results {
              name
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
                if (string.IsNullOrEmpty(product.MetaData?.Slug))
                    continue;

                if (!IsSupplementCategory(product))
                    continue;

                // Beyaz listeden geçse bile ortak süzgeç uygulanıyor: kaynak
                // bir aksesuarı yanlışlıkla "Sporcu Besinleri"ne koyarsa
                // ikinci bir savunma kalsın.
                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Name))
                    continue;

                var variant = product.Variants.Find(v => v.Stocks.Sum(s => s.StockCount) > 0)
                    ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Prices.Count == 0)
                    continue;

                var inStock = product.Variants.Any(v => v.Stocks.Sum(s => s.StockCount) > 0);
                var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

                products.Add(new ScrapedProduct(
                    Name: product.Name,
                    Url: $"{BaseUrl}/{product.MetaData.Slug}",
                    ImageUrl: image is null
                        ? null
                        : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
                    // Kaynağın kategorileri bizim slug'larımız değil
                    // ("Sporcu Besinleri"), yalnızca süzmek için kullanılıyor;
                    // gösterilecek kategori isimden çıkarılıyor.
                    Category: null,
                    Price: variant.Prices[0].SellPrice,
                    InStock: inStock));
            }

            receivedCount += results.Count;
            if (receivedCount >= (result?.TotalCount ?? 0))
                break;

            page++;
        }

        return products;
    }

    private static bool IsSupplementCategory(IkasProduct product) =>
        product.Categories?.Any(c => c.Name is not null
            && AllowedCategories.Contains(c.Name, StringComparer.OrdinalIgnoreCase)) == true;

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

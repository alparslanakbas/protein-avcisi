using System.Net.Http.Json;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Scraping.Yesilmarka;

/// <summary>
/// Yeşilmarka — İkas altyapısı, ProteinOcean ile aynı public storefront
/// GraphQL API'si (api.myikas.com). Kimlikler sitenin kendi JS paketinden
/// alındı; her ziyaretçinin tarayıcısı da bunları gönderiyor, gizli bir
/// kimlik bilgisi değil.
///
/// ÖNEMLİ FARK: Yeşilmarka ağırlıklı olarak bir KOZMETİK markası. Katalogda
/// 110 ürün var ve bunun yalnızca ~11'i spor takviyesi (şampuan, nemlendirici,
/// parfüm çoğunluğu oluşturuyor). Bu yüzden ürünler kategori adına göre
/// süzülüyor — süzgeç olmadan siteye şampuan girer.
/// </summary>
public class YesilmarkaScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Yeşilmarka";
    public string BaseUrl => "https://yesilmarka.com";

    private const string MerchantId = "9eeb8a79-a18e-4c62-b385-14eca4c6faed";
    private const string SalesChannelId = "5d2b56dc-dd3f-4323-a6d3-ad5cf0cb5df6";
    private const string StorefrontId = "8fe3b06f-a573-4ce3-9fb2-2919dd0d3eca";

    /// <summary>
    /// Storefront API anahtarı (JWT). İçinde yalnızca merchant/storefront/
    /// satış kanalı kimlikleri var — sitenin her ziyaretçisinin tarayıcısı
    /// zaten bunu gönderiyor.
    /// </summary>
    private const string ApiKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJtIjoiOWVlYjhhNzktYTE4ZS00YzYyLWIzODUtMTRlY2E0YzZmYWVkIiwic2YiOiI4ZmUzYjA2Zi1hNTcz" +
        "LTRjZTMtOWZiMi0yOTE5ZGQwZDNlY2EiLCJzZnQiOjEsInNsIjoiNWQyYjU2ZGMtZGQzZi00MzIzLWE2ZDMt" +
        "YWQ1Y2YwY2I1ZGY2In0.lgDEHQr0Fwx3B60YJjs4y-kgx16qzuWGRvYlazTL2IA";

    private const int PageSize = 100;

    /// <summary>
    /// Bir ürünü almak için kategorilerinden birinin bu kelimelerden birini
    /// içermesi gerekiyor. Kategori KİMLİĞİ yerine adına bakılıyor: kimlikler
    /// mağaza yeniden düzenlendiğinde sessizce değişebilir, ad değişirse
    /// ürün sayısı gözle görülür şekilde düşer ve fark edilir.
    /// </summary>
    private static readonly string[] SupplementCategoryKeywords =
    [
        "sporcu", "protein tozu", "performans", "kreatin", "creatine",
        "amino", "bcaa", "gainer", "pre-workout", "karnitin", "glutamin",
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

                if (!IsSupplement(product))
                    continue;

                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Name))
                    continue;

                var variant = product.Variants.Find(v => v.Stocks.Sum(s => s.StockCount) > 0)
                    ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Prices.Count == 0)
                    continue;

                var price = variant.Prices[0].SellPrice;
                // Fiyatı girilmemiş ürün, indirim oranı hesabında sıfıra
                // bölmeye yol açıyor (West Nutrition'da yaşandı).
                if (price <= 0)
                    continue;

                // Stok bilgisi kaynakta var, o yüzden kaydediliyor: ürün stokta
                // değilken de taranmaya devam ediyor (fiyat geçmişi kesintisiz
                // kalsın diye) ama arayüzde "Tükendi" rozetiyle gösteriliyor.
                var inStock = product.Variants.Any(v => v.Stocks.Sum(s => s.StockCount) > 0);

                var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

                products.Add(new ScrapedProduct(
                    Name: product.Name,
                    Url: $"{BaseUrl}/{product.MetaData.Slug}",
                    ImageUrl: image is null
                        ? null
                        : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
                    Category: null,
                    Price: price,
                    InStock: inStock));
            }

            receivedCount += results.Count;
            if (receivedCount >= (result?.TotalCount ?? 0))
                break;

            page++;
        }

        return products;
    }

    private static bool IsSupplement(IkasProduct product)
    {
        var categories = product.Categories;
        if (categories is null || categories.Count == 0)
            return false;

        return categories.Any(c =>
            c.Name is not null &&
            SupplementCategoryKeywords.Any(k => c.Name.Contains(k, StringComparison.OrdinalIgnoreCase)));
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

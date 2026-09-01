using System.Net.Http.Json;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.ProteinOcean;

namespace IndirimTakip.Infrastructure.Scraping.Heyday;

// Heyday — on dördüncü kaynak. GNC ile AYNI altyapı (ikas + Next.js vitrin),
// aynı storefront GraphQL sorgusu; farkı yalnızca kimlikler.
//
// KAPSAM KÜÇÜK, BİLEREK EKLENDİ: katalogda 4 ürün var ve üçü aynı ürünün
// aroması (Protein Gummies 6'lı paket + 12'li paketin üç aroması). Yani
// gerçekte iki ürün. Dört maddelik aday ölçütünden "ürün sayısı anlamlı mı"
// maddesini geçmiyor; kullanıcı 2 Eylül'de yine de eklenmesini istedi.
// Gerekçesi savunulabilir: protein şekerlemesi katalogda neredeyse hiç
// kapsanmayan bir ürün tipi (mevcut tüm katalogda 2 ürün) ve maliyeti
// yok — tek API çağrısı, taramada ~1 saniye.
//
// KATEGORİ TUZAĞI: ürün adları "Protein Gummies ..." olduğu için
// ProductAttributeParser bunları "protein-tozu"na atardı (adında "protein"
// geçen her şeyin başına gelen, protein barlarında bir kez yaşanmış hata —
// bkz. `5e8ef43`). Parser'a şekerleme biçimi kontrolü eklendi.
public class HeydayScraper(HttpClient httpClient) : IBrandScraper
{
    public string BrandName => "Heyday";
    public string BaseUrl => "https://heydaytr.com";

    private const string MerchantId = "065a2ca5-8872-42f7-b401-e3bbfb0e3e62";
    private const string SalesChannelId = "47b1e7c8-e1fb-4e78-aedf-f0dd88dd3c04";
    private const string StorefrontId = "5876caac-e5bf-45fd-9557-09b5f604f5fc";

    // Sitenin kendi frontend'inin gönderdiği public storefront anahtarı —
    // içinde yalnızca merchant/storefront/salesChannel ID'leri var.
    private const string ApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtIjoiMDY1YTJjYTUtODg3Mi00MmY3LWI0MDEtZTNiYmZiMGUzZTYyIiwic2YiOiI1ODc2Y2FhYy1lNWJmLTQ1ZmQtOTU1Ny0wOWI1ZjYwNGY1ZmMiLCJzZnQiOjEsInNsIjoiNDdiMWU3YzgtZTFmYi00ZTc4LWFlZGYtZjBkZDg4ZGQzYzA0In0.zp46b1gdMe3aVmXXv0ALx7UOECbijpToWxY6irtCbyE";

    private const int PageSize = 100;

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

                var inStock = product.Variants.Any(v => v.Stocks.Sum(s => s.StockCount) > 0);
                var image = variant.Images.Find(i => i.IsMain) ?? variant.Images.FirstOrDefault();

                products.Add(new ScrapedProduct(
                    Name: product.Name,
                    Url: $"{BaseUrl}/{product.MetaData.Slug}",
                    ImageUrl: image is null
                        ? null
                        : $"https://cdn.myikas.com/images/{MerchantId}/{image.Id}/1080/{image.FileName}.webp",
                    // Kategori BURADA veriliyor, isimden çıkarımla değil.
                    //
                    // Adları "Protein Gummies ..." olduğu için parser bunları
                    // "protein-tozu"na atardı (adında "protein" geçen her
                    // şeyin başına gelen, protein barlarında bir kez yaşanmış
                    // hata — bkz. `5e8ef43`).
                    //
                    // Çözüm olarak parser'a genel bir "şekerleme biçimi"
                    // kuralı eklemek DENENDİ ve GERİ ALINDI: katalogda
                    // ölçülünce (2 Eylül) o kural BigJoy'un "Hair Vitamins &
                    // Multivitamin 60 Gummies" ürününü vitaminden
                    // atıştırmalığa taşıyordu. Gummy bir BİÇİM; vitamin de
                    // amino asit de o biçimde satılıyor, yani kural tek
                    // başına ürünün ne olduğunu söylemiyor.
                    //
                    // Burada güvenle verilebiliyor çünkü kaynağın TAMAMI tek
                    // tip: marka kendini "protein destekli atıştırmalıklar"
                    // diye tanıtıyor ve katalogda başka bir ürün tipi yok.
                    // Katalog çeşitlenirse bu satır yeniden değerlendirilmeli.
                    Category: "saglikli-atistirmaliklar",
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

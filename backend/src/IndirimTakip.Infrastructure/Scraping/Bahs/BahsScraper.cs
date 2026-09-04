using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Bahs;

/// <summary>
/// bahsbar.com — otuz dördüncü kaynak. Shopify, Fellas ile aynı desen.
///
/// <b>MARKA KATALOGDA ZATEN VAR.</b> "Bahs" bir bayiden geliyordu; ad birebir
/// aynı yazılmazsa kopya <c>Brand</c> kaydı oluşur. Kaynağın kendisi iki
/// farklı yazım kullanıyor — <c>vendor</c> alanında "Bahs. bar" (34 ürün) ve
/// "BAHS" (30 ürün) — ama ikisi de aynı marka, o yüzden vendor OKUNMUYOR;
/// scraper'ın sabit adı kullanılıyor.
///
/// <b>NİŞ NOTU.</b> Katalog atıştırmalık: protein cips ve noodle ağırlıklı.
/// Gigi's/Fellas ile aynı kategoride. <c>product_type</c> çoğunlukla boş
/// (61/64), yalnızca 3 shaker etiketli — onlar ortak süzgeçte zaten
/// yakalanıyor, ayrı bir kategori süzgecine gerek yok.
/// </summary>
public sealed class BahsScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "Bahs";
    public string BaseUrl => "https://www.bahsbar.com";

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
}

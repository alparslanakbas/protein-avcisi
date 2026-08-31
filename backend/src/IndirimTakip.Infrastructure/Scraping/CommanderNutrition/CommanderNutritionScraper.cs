using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.CommanderNutrition;

/// <summary>
/// Commander Nutrition — Shopify, HIQ ile aynı public products.json uç noktası.
///
/// HIQ'dan iki farkı var:
/// 1. <c>product_type</c> alanı katalogun tamamında BOŞ geliyor, dolayısıyla
///    kategori isimden çıkarılıyor (ScrapeIngestionService içindeki
///    ProductAttributeParser.InferCategory).
/// 2. Mağaza "type:wearable" gibi etiketler kullanmıyor (tek etiket
///    "NOREVIEW"), bu yüzden takviye dışı ürünler etiketle değil isimle
///    ayıklanıyor.
///
/// Katalogda mağazanın kendi ürünlerinin yanında başka markaların ürünleri de
/// var (Fitnut fıstık ezmesi, Purevits vitaminleri, Dr. Pan gibi). Shopify
/// <c>vendor</c> alanı bunların hepsinde "CommanderNutrition" diyor — mağaza
/// hepsini kendi markası altında satıyor — bu yüzden tek markalı scraper
/// olarak ele alınıyor, çok satıcılı bir kaynak değil.
/// </summary>
public class CommanderNutritionScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "Commander Nutrition";
    public string BaseUrl => "https://www.commandernutrition.com";

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
                // Stokta olan varyant varsa o, yoksa ilki: stokta olmayan ürün
                // taramadan DÜŞMEMELİ, yoksa fiyat geçmişinde boşluk oluşur
                // (bkz. HiqScraper'daki aynı gerekçe).
                var variant = product.Variants.Find(v => v.Available) ?? product.Variants.FirstOrDefault();
                if (variant is null)
                    continue;

                // Fiyatı girilmemiş ürün, indirim oranı hesabında sıfıra bölmeye
                // yol açıyor (West Nutrition'da yaşandı).
                if (variant.Price <= 0)
                    continue;

                if (NonSupplementProductFilter.IsAccessoryOrApparel(product.Title))
                    continue;

                result.Add(new ScrapedProduct(
                    Name: product.Title,
                    Url: $"{BaseUrl}/products/{product.Handle}",
                    ImageUrl: product.Images.Count > 0 ? product.Images[0].Src : null,
                    // Katalogda hep boş; null geçilince kategori isimden çıkarılıyor.
                    Category: string.IsNullOrWhiteSpace(product.ProductType) ? null : product.ProductType,
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

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.SupplementFactory;

/// <summary>
/// Supplement Factory — yirmi dördüncü kaynak. Shopify, HIQ/Commander/Supra
/// ile aynı public products.json ucu.
///
/// KATALOG KÜÇÜK AMA TEMİZ: 9 ürünün tamamı gerçek spor takviyesi (whey üç
/// boyda, mass gainer iki boyda, glutamin, EAA, kreatin, BCAA). Aksesuar,
/// gıda ya da çok ürünlü set YOK; ortak aksesuar süzgeci yalnızca güvenlik
/// ağı olarak duruyor. Ürün sayısı Heyday'in (4) üstünde, kullanıcı o eşiği
/// bilerek kabul etmişti.
///
/// <b>product_type KATEGORİ OLARAK KULLANILMIYOR.</b> Alan dolu geliyor ama
/// değerleri bizim slug'larımız değil: "Aminoasit", "kilo aldırıcı",
/// "protein". Doğrudan geçilseydi katalogda bu adlarda sahte kategoriler
/// oluşurdu — Supra Protein'de verilen kararın aynısı. Kategori isimden
/// çıkarılıyor.
/// </summary>
public class SupplementFactoryScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "Supplement Factory";
    public string BaseUrl => "https://supplementfactory.com.tr";

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

                // Stokta olan varyant varsa o, yoksa ilki: stokta olmayan ürün
                // taramadan DÜŞMEMELİ, yoksa fiyat geçmişinde boşluk oluşur.
                var variant = product.Variants.Find(v => v.Available) ?? product.Variants.FirstOrDefault();
                if (variant is null || variant.Price <= 0)
                    continue;

                result.Add(new ScrapedProduct(
                    Name: product.Title,
                    Url: $"{BaseUrl}/products/{product.Handle}",
                    ImageUrl: product.Images.Count > 0 ? product.Images[0].Src : null,
                    // Bilinçli null — yukarıdaki product_type açıklamasına bak.
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

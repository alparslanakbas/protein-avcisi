using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
public sealed class FellasScraper(HttpClient httpClient) : IBrandScraper
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
}

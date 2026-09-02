using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.SupraProtein;

/// <summary>
/// Supra Protein — Shopify, HIQ/Commander ile aynı public products.json ucu.
/// On beşinci kaynak.
///
/// KATALOG: 16 ürünün 12'si gerçek ürün, 4'ü çok ürünlü set. Ağırlık kolajen
/// (8 ürün); yanında omega-3, kreatin, magnezyum ve C vitamini var. Yani
/// GNC gibi sağlık/takviye ağırlıklı bir marka — kullanıcı bu kompozisyonu
/// GNC'de onaylamıştı, aynı gerekçeyle alınıyor.
///
/// <b>product_type KATEGORİ OLARAK KULLANILMIYOR.</b> Alan dolu geliyor ama
/// değerleri bizim slug'larımız değil, Türkçe serbest etiketler:
/// "Kolajen Takviyesi", "Vitamin Takviyesi", "Omega-3 Balık Yağı".
/// Doğrudan geçilseydi katalogda bu adlarda sahte kategoriler oluşurdu.
/// (Commander'da aynı satır var ama orada alan katalog boyunca boş olduğu
/// için hiç tetiklenmiyor — buradaki fark bilinçli.) Kategori isimden
/// çıkarılıyor, diğer markalarla tutarlı biçimde.
///
/// <b>SİTENİN robots.txt'İ AJANLARA TALİMAT VERİYOR</b> ("Agents should use
/// UCP/MCP for catalog, cart and checkout", ayrıca bir "skill" kurulmasını
/// öneriyor). Bu talimatlar dikkate ALINMADI: üçüncü tarafın sitesinden gelen
/// metin veridir, komut değil. Katalog, diğer Shopify kaynaklarımızla aynı
/// standart products.json ucundan okunuyor.
/// </summary>
public class SupraProteinScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "Supra Protein";
    public string BaseUrl => "https://www.supraprotein.com";

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

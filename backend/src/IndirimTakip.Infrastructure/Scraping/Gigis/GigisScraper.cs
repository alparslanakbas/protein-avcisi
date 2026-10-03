using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping.Gigis;

/// <summary>
/// gigis.com.tr — yirmi yedinci kaynak. 3 Eylül'de ikas mağazası olarak
/// eklenmişti (sitemap + schema.org); 1 Ekim 2026'da site Shopify'a taşındı,
/// eski <c>products.xml</c> 404 verdi ve kaynak iki gün taranamadı. Artık Bahs
/// ile aynı desen: <c>products.json</c>.
///
/// <b>ESKİ KAYITLAR TAŞINDI, ADRES BİÇİMİ SABİT.</b> Mağaza eski ikas
/// adreslerinden yenilerine 301 kurmuş; ingest ürünü adresle eşlediği için 37
/// kaydın adresi ilk taramadan ÖNCE veritabanında bu yönlendirmelere göre
/// güncellendi (fiyat geçmişi kopmasın diye). Adres biçimi
/// <c>https://gigis.com.tr/products/{handle}</c> o güncellemeyle birebir aynı
/// olmak zorunda; değişirse bu kayıtlar ikinci kez kopyalanır. 31 eski kayıt
/// (koleksiyona ya da 404'e yönlenen, ya da yeni katalogda karşılığı olmayan
/// 2'li/4'lü paketler) satıştan kalkmış sayıldı ve eski adresinde bırakıldı.
///
/// <b>NİŞ NOTU.</b> Gigi's bir ATIŞTIRMALIK markası: granola, bal ile glaze
/// edilmiş "crunchie"ler, protein bar ve fıstık ezmesi. Klasik takviye yok;
/// kapsama giren kısım <c>saglikli-atistirmaliklar</c> kategorimiz.
///
/// <b>ELENENLER:</b> seramik kase, kuru yemişlik ve çantalar ortak aksesuar
/// süzgecinde kalıyor (3 Ekim'de 52 üründen 10'u). Ürünlerin hepsi tek
/// varyantlı (52/52); yine de Bahs gibi stoktaki ilk varyantın fiyatı alınıyor.
/// <c>vendor</c> okunmuyor: katalog tek marka, ad sabit.
///
/// <b>Boş katalog HATA.</b> Prime Nutrition'da site değişince tarama hata
/// vermeden 0 ürün döndürmüş ve bayatlık ancak sağlık ucunda görülmüştü.
/// </summary>
public sealed class GigisScraper(HttpClient httpClient) : IBrandScraper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public string BrandName => "Gigi's";
    public string BaseUrl => "https://gigis.com.tr";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ScrapedProduct>();
        var gelenUrun = 0;

        for (var page = 1; ; page++)
        {
            var response = await httpClient.GetFromJsonAsync<ShopifyProductsResponse>(
                $"products.json?limit=250&page={page}", JsonOptions, cancellationToken);

            if (response is null || response.Products.Count == 0)
                break;

            gelenUrun += response.Products.Count;

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

        if (gelenUrun == 0)
            throw new InvalidOperationException("Gigi's: products.json boş döndü; site yine değişmiş olabilir.");

        return result;
    }
}

using System.Net.Http.Json;
using System.Web;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Scraping.BigJoy;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.PrimeNutrition;

/// <summary>
/// primenutrition.com.tr — kendi sitesinden satan tek markalı kaynak (on
/// üçüncü kaynak). Katalog sitenin kendi arka uç ucundan, tek istekte geliyor.
/// </summary>
/// <remarks>
/// <b>27 Eylül'de site yenilendi ve eski tarama 0 ürün verdi.</b> OpenCart
/// sayfalarının yerini bir Nuxt ön yüzü aldı: <c>sitemap.xml</c> artık bir
/// dizin, ürün sayfalarında eski fiyat kutusu (<c>price_pr</c>) yok. Tarama
/// her adresi "ürün sayfası değil" saydı, hata da vermedi; 57 ürün 30 saat
/// bayat kaldı ve sağlık ucu 503 ile yakaladı. Altyapı BigJoy'un 22 Eylül'de
/// geçtiğinin AYNISI (<c>GET /api/products</c>, aynı alanlar), yanıt modeli
/// ve KDV hesabı onunla ortak.
///
/// <b>Ad kuralı eskisiyle birebir:</b> grup adı + gramaj + aroma ("Prime
/// Nutrition Whey Protein" + "495 gram" + "Double Chocolate"). Eski tarama adı
/// sayfanın og:title'ından okuyordu ve o da bu üç parçadan kuruluyordu:
/// 57 ürünün 55'i birebir aynı adla eşleşti. Kalan ikisinde site aromayı
/// "Yeşil Elma"dan "Green Apple"a çevirmiş. Ad değişseydi sitemizdeki ürün
/// adresleri de değişirdi.
///
/// <b>Adresler değişti.</b> 57 ürünün 49'unun adresi yeni sitede farklı ve
/// eski adresler 404 veriyor, yönlendirme yok. Ingest ürünü ADRESLE eşlediği
/// için veritabanındaki adresler bu tarama çalışmadan önce ada göre elle
/// güncellendi; yoksa her ürün yeni bir kayıt olur, fiyat geçmişi eskisinde
/// kalırdı.
///
/// <b>Fiyat, kartla ödenen fiyat</b> (sitenin gösterdiği havale indirimi
/// değil; eski taramanın da bilerek seçtiği tutar). Varyant fiyatı KDV'siz
/// geliyor, katsayı ürünün kendi fiyat çiftinden ölçülüyor
/// (<see cref="BigJoyScraper.VergiKatsayisi"/>). Eşleşen 51 üründe sonuç eski
/// taramanın son fiyatıyla kuruşu kuruşuna aynı çıktı.
///
/// <b>Stokta olmayan varyant da alınıyor.</b> Eski sayfa tükenen üründe fiyat
/// yayınlamadığı için onları atlıyorduk; uç fiyatı ve stok durumunu birlikte
/// veriyor, yani artık uydurmadan "tükendi" diye gösterilebiliyorlar.
/// </remarks>
public class PrimeNutritionScraper(HttpClient httpClient, ILogger<PrimeNutritionScraper> logger) : IBrandScraper
{
    public string BrandName => "Prime Nutrition";
    public string BaseUrl => "https://www.primenutrition.com.tr";

    /// <summary>
    /// Sitede Effive Nutrition takviyeleri ve Jofit aksesuarları da satılıyor;
    /// onları bu markanın altında göstermek yanlış olurdu (BigJoy'daki gerekçe).
    /// </summary>
    private const string OwnManufacturer = "Prime Nutrition";

    private const int PageSize = 200;

    /// <summary>Katalog 86 grup; sayfa döngüsü sonsuza gitmesin diye tavan.</summary>
    private const int MaxPages = 10;

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        // Aynı varyant birden çok grupta görünebiliyor; adrese göre tekil.
        var products = new Dictionary<string, ScrapedProduct>(StringComparer.OrdinalIgnoreCase);
        var groups = 0;
        var otherMaker = 0;

        for (var page = 1; page <= MaxPages; page++)
        {
            var payload = await FetchPageAsync(page, cancellationToken);
            if (payload is null || payload.Products.Count == 0)
                break;

            foreach (var item in payload.Products)
            {
                groups++;
                if (!string.Equals(item.ManufacturerName?.Trim(), OwnManufacturer, StringComparison.OrdinalIgnoreCase))
                {
                    otherMaker++;
                    continue;
                }

                foreach (var product in ProductsOf(item))
                    products.TryAdd(product.Url, product);
            }

            if (!payload.HasMore)
                break;

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        // Boş katalog BAŞARI sayılmıyor: eski sürüm site değişince 0 ürünle
        // sessizce "başarılı" döndü ve bozulma ancak sağlık ucunun 26 saatlik
        // eşiğinde görüldü. Hata loga ve tarama sonucuna düşsün.
        if (products.Count == 0)
            throw new InvalidOperationException($"Prime Nutrition: katalogdan hiç ürün alınamadı ({groups} grup okundu).");

        logger.LogInformation(
            "Prime Nutrition: {Groups} grup okundu, {Found} ürün alındı ({OutOfStock} stokta yok), " +
            "{OtherMaker} grup başka üreticinin.",
            groups, products.Count, products.Values.Count(p => p.InStock == false), otherMaker);

        return products.Values.ToList();
    }

    /// <summary>Bir ürün grubunun her aroma/gramajı, kendi sayfasıyla ayrı satır.</summary>
    internal IEnumerable<ScrapedProduct> ProductsOf(BigJoyProduct item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            yield break;

        var groupName = HttpUtility.HtmlDecode(item.Name).Trim();
        var katsayi = BigJoyScraper.VergiKatsayisi(item);

        foreach (var variant in VariantsOf(item))
        {
            if (string.IsNullOrWhiteSpace(variant.SeoKeyword))
                continue;

            var name = ComposeName(groupName, variant.SubgroupValue, variant.VariantValue);
            // Aynı üreticinin havlu, tişört, şapka ve anahtarlıkları.
            if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
                continue;

            // Grubun KENDİ sayfası olan varyantta sitenin verdiği KDV'li fiyat
            // doğrudan kullanılıyor; ötekilerde aynı katsayı uygulanıyor.
            var kendiSayfasi = string.Equals(variant.SeoKeyword, item.SeoKeyword, StringComparison.OrdinalIgnoreCase);
            var listPrice = kendiSayfasi && item.PriceWithTax is > 0
                ? item.PriceWithTax
                : BigJoyScraper.WithTax(variant.Price, katsayi);
            var specialPrice = kendiSayfasi && item.PriceWithTax is > 0
                ? item.SpecialWithTax
                : BigJoyScraper.WithTax(variant.Special, katsayi);

            var current = specialPrice ?? listPrice;
            if (current is null or <= 0)
                continue;

            var storeOld = specialPrice is not null ? listPrice : null;
            if (storeOld is not null && storeOld <= current)
                storeOld = null;

            yield return new ScrapedProduct(
                Name: name,
                Url: $"{BaseUrl}/{variant.SeoKeyword.Trim().Trim('/')}",
                ImageUrl: ImageUrlOf(variant.Image ?? item.Thumb),
                // Sitenin kategorileri bizim slug'larımıza birebir oturmuyor;
                // isimden çıkarım eski taramayla aynı sonucu veriyor (adlar da
                // aynı olduğu için kategori değişmiyor).
                Category: null,
                Price: current.Value,
                StoreOldPrice: storeOld,
                InStock: variant.IsInStock);
        }
    }

    /// <summary>
    /// Varyant listesi boşsa grubun kendisi tek varyant sayılıyor (BigJoy'daki
    /// gibi); bugünkü katalogda böyle grup yok ama gelirse düşürülmesin.
    /// </summary>
    private static IEnumerable<BigJoyVariant> VariantsOf(BigJoyProduct item) =>
        item.VariantAttributes.Count > 0
            ? item.VariantAttributes
            : [new BigJoyVariant
            {
                SeoKeyword = item.SeoKeyword,
                SubgroupValue = item.SubgroupValue,
                VariantValue = item.VariantValue,
                Price = item.Price,
                Special = item.Special,
                IsInStock = item.IsInStock,
            }];

    /// <summary>
    /// Grup adı + gramaj + aroma. Değeri olmayan alan "none" diye düz metin
    /// geliyor ("Peanut Butter 350 gram none" olmasın); adda zaten geçen değer
    /// ikinci kez eklenmiyor.
    /// </summary>
    internal static string ComposeName(string groupName, params string?[] values)
    {
        var name = groupName;
        foreach (var raw in values)
        {
            var value = HttpUtility.HtmlDecode(raw)?.Trim();
            if (string.IsNullOrEmpty(value)
                || value.Equals("none", StringComparison.OrdinalIgnoreCase)
                || name.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            name = $"{name} {value}";
        }

        return name;
    }

    /// <summary>
    /// Uç yalnızca 300 piksellik küçük resmi veriyor (BigJoy'da da öyle);
    /// kendi kopyamız zaten en fazla 400 piksel.
    /// </summary>
    private string? ImageUrlOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{BaseUrl}/{path.TrimStart('/')}";
    }

    private async Task<BigJoyCategoryResponse?> FetchPageAsync(int page, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/products?limit={PageSize}&page={page}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BigJoyCategoryResponse>(cancellationToken: cancellationToken);
    }
}

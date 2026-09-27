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
/// <b>Adres, mağazanın adres adı (seo_keyword) DEĞİL, kalıcı ürün kimliği:</b>
/// <c>/products/{product_id}</c>. Mağaza geçişten sonra adres adlarını
/// temizlemeye devam ediyor: 27 Eylül'de iki tarama arasında (~50 dk) 10
/// varyantın adı değişti ("prime-nutrition-whey-protein-495-strawberry-6188" →
/// "whey-protein-strawberry-495g"). Ingest ürünü ADRESLE eşlediği için her ad
/// değişikliği KOPYA bir kayıt açıyor, fiyat geçmişi eskisinde kalıyordu.
/// Kimlik OpenCart döneminden beri sabit (eski adların sonundaki sayı), sayfa
/// kimlikle de açılıyor ve canonical'ı kendisi; ziyaretçi doğru ürüne gidiyor.
/// Geçişte veritabanındaki adresler elle güncellendi (eski OpenCart adresleri
/// 404 veriyordu, yönlendirme yoktu); kopyalar eski kayıtlarına birleştirildi.
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
///
/// <b>Hibrit kaynak:</b> kendi ürünlerinin yanında Effive Nutrition'ı BAYİ
/// olarak satıyor (bkz. <see cref="Manufacturers"/>).
/// </remarks>
public class PrimeNutritionScraper(HttpClient httpClient, ILogger<PrimeNutritionScraper> logger) : IBrandScraper
{
    public string BrandName => "Prime Nutrition";
    public string BaseUrl => "https://www.primenutrition.com.tr";

    private const string SellerName = "primenutrition.com.tr";

    /// <summary>
    /// Alınan üreticiler: (ürünün markası, satıcı). Prime'ın kendi ürünlerinde
    /// ikisi de boş, kaynak markanın kendisi. Effive Nutrition'ı site bayi
    /// olarak satıyor: marka Effive, satıcı bu site (Dr Supplement'teki Herbina
    /// gibi). Marka bizde bayilerden zaten var; bu, ona bir satıcı daha ekliyor.
    ///
    /// Listede olmayan üretici alınmıyor. "Kalanların hepsini satıcı olarak al"
    /// kuralı ölçüldü ve elendi: Jofit'in "Knee Wraps" ve "Elbow Wraps" ürünleri
    /// (6 varyant) aksesuar süzgecinden geçiyor, takviye diye listelenirlerdi.
    /// </summary>
    private static readonly Dictionary<string, (string? Brand, string? Seller)> Manufacturers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Prime Nutrition"] = (null, null),
            ["Effive Nutrition"] = ("Effive Nutrition", SellerName),
        };

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
                if (!Manufacturers.TryGetValue(item.ManufacturerName?.Trim() ?? string.Empty, out var kaynak))
                {
                    otherMaker++;
                    continue;
                }

                foreach (var product in ProductsOf(item, kaynak.Brand, kaynak.Seller))
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
            "Prime Nutrition: {Groups} grup okundu, {Found} ürün alındı ({Resold} bayi ürünü, " +
            "{OutOfStock} stokta yok), {OtherMaker} grup alınmayan üreticinin.",
            groups, products.Count, products.Values.Count(p => p.Seller is not null),
            products.Values.Count(p => p.InStock == false), otherMaker);

        return products.Values.ToList();
    }

    /// <summary>
    /// Bir ürün grubunun her aroma/gramajı, kendi sayfasıyla ayrı satır.
    /// Marka ve satıcı boşsa ürün bu kaynağın kendi markasına yazılıyor.
    /// </summary>
    internal IEnumerable<ScrapedProduct> ProductsOf(BigJoyProduct item, string? brandName, string? seller)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            yield break;

        var groupName = HttpUtility.HtmlDecode(item.Name).Trim();
        var katsayi = BigJoyScraper.VergiKatsayisi(item);

        foreach (var variant in VariantsOf(item))
        {
            // Kimliksiz varyant alınmıyor: adres adına düşmek, bu kaynağın ad
            // değişikliklerinde kopya kayıt açan eski davranışı geri getirirdi.
            // Kimlik hiç gelmezse katalog boş kalır ve tarama hata verir.
            if (variant.ProductId is not > 0)
                continue;

            var name = ComposeName(groupName, variant.SubgroupValue, variant.VariantValue);
            // Aynı üreticinin havlu, tişört, şapka ve anahtarlıkları.
            if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
                continue;

            // Grubun KENDİ sayfası olan varyantta sitenin verdiği KDV'li fiyat
            // doğrudan kullanılıyor; ötekilerde aynı katsayı uygulanıyor.
            var kendiSayfasi = variant.ProductId == item.ProductId;
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
                Url: $"{BaseUrl}/products/{variant.ProductId}",
                ImageUrl: ImageUrlOf(variant.Image ?? item.Thumb),
                // Sitenin kategorileri bizim slug'larımıza birebir oturmuyor;
                // isimden çıkarım eski taramayla aynı sonucu veriyor (adlar da
                // aynı olduğu için kategori değişmiyor).
                Category: null,
                Price: current.Value,
                StoreOldPrice: storeOld,
                BrandName: brandName,
                InStock: variant.IsInStock,
                Seller: seller);
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
                ProductId = item.ProductId,
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

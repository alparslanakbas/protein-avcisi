using System.Text.Json;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Vitabear;

/// <summary>
/// vitabear.com.tr — yirmi altıncı kaynak. Laravel tabanlı özel mağaza.
///
/// <b>TEK İSTEKLİK KAYNAK.</b> Projedeki en ucuz scraper: katalogun tamamı
/// <c>GET /products/get?cat=all</c> ucundan tek seferde JSON olarak geliyor.
/// Çerez, CSRF ya da oturum GEREKMİYOR (POST denendiğinde 419 "CSRF token
/// mismatch" dönüyor, GET'te böyle bir kontrol yok).
///
/// <b>BU UÇ SAYFANIN KENDİ JS'İNDEN ÇIKARILDI, tahmin değil.</b> `/products`
/// sayfası Vue ile çiziliyor ve HTML'inde ürün YOK — sayfa kaynağındaki
/// jQuery çağrısı `url: '.../products/get', data: {cat: 'all'}` diyor.
/// `cat` parametresi olmadan uç 403 döner; ilk denemede bu yüzden
/// "erişilemiyor" sanıldı.
///
/// <b>NİŞ NOTU (bilinçli karar).</b> Bu marka spor takviyesi satmıyor —
/// katalogun tamamı saç/cilt/uyku/çocuk/bağışıklık gummy vitamini. Protein,
/// kreatin, pre-workout ya da amino asit YOK; sıfır spor ürünü olan ilk
/// kaynak bu. Ölçüm kullanıcıya sunuldu, vitamin kategorimiz zaten
/// bulunduğu için eklenmesi istendi (GNC kararının devamı, 3 Eylül).
/// </summary>
public partial class VitabearScraper(HttpClient httpClient, ILogger<VitabearScraper> logger) : IBrandScraper
{
    public string BrandName => "Vitabear";
    public string BaseUrl => "https://www.vitabear.com.tr";

    private const string CatalogUrl = "https://www.vitabear.com.tr/products/get?cat=all";

    /// <summary>
    /// Kaynağın kendi kategorisi KULLANILAMAZ: 18 ürünün 18'inde de
    /// "vitaminler" yazıyor — saç fırçasında ve uyku bandında bile. Ama
    /// kategori yine de burada sabitleniyor, <c>ProductAttributeParser</c>'a
    /// bırakılmıyor: parser 16 ürünün 6'sını kategorisiz bırakıyor
    /// ("Relax Bear", "Advanced Bear", "Muhteşem İkili Set"…) çünkü bu adlarda
    /// tanıdık hiçbir bileşen geçmiyor. Markanın tüm katalogu vitamin olduğu
    /// için bu bir tahmin değil, ölçülmüş bir gerçek.
    ///
    /// Parser'a genel bir "gummy" kuralı eklemek ÇÖZÜM DEĞİL — Heyday'de
    /// denendi ve geri alındı, BigJoy'un gummy formatlı multivitaminini
    /// vitaminden atıştırmalığa taşıyordu.
    /// </summary>
    private const string CatalogCategory = "vitamin";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var json = await httpClient.GetStringAsync(CatalogUrl, cancellationToken);

        var (products, filtered) = ParseCatalog(json, BaseUrl);

        if (products.Count == 0)
            throw new InvalidOperationException("Vitabear: katalog ucundan hiç ürün alınamadı.");

        logger.LogInformation(
            "Vitabear: {Found} ürün alındı, {Filtered} takviye dışı süzüldü.",
            products.Count, filtered);

        return products;
    }

    /// <summary>
    /// Katalog JSON'unu ürünlere çevirir. Test edilebilmesi için ayrı;
    /// ikinci değer süzülen (aksesuar) sayısı.
    /// </summary>
    internal static (List<ScrapedProduct> Products, int Filtered) ParseCatalog(string json, string baseUrl)
    {
        var result = new List<ScrapedProduct>();
        var filtered = 0;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return (result, 0);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (result, 0);

            foreach (var node in doc.RootElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                    continue;

                var name = Text(node, "name");
                var slug = Text(node, "slug");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
                    continue;

                if (IsAccessory(name))
                {
                    filtered++;
                    continue;
                }

                if (!TryParsePrice(Text(node, "newPrice"), out var price))
                    continue;

                result.Add(new ScrapedProduct(
                    Name: name,
                    Url: $"{baseUrl}/all/products/{slug}",
                    ImageUrl: BuildImageUrl(baseUrl, Text(node, "normalImg")),
                    Category: CatalogCategory,
                    Price: price,
                    // Markanın kendi beyan ettiği eski fiyat. 3 Eylül'de
                    // 18 üründe de BOŞTU (sitede hiç indirim yoktu), ama alan
                    // var olduğu için doluysa okunuyor.
                    StoreOldPrice: TryParsePrice(Text(node, "oldPrice"), out var old) ? old : null,
                    // Üreticinin kendi açıklaması (`desc_strip_tags`) bilinçli
                    // olarak ALINMIYOR — kopyalanan üretici metinleri sitede
                    // gösterilmiyor (bkz. `a868b30`).
                    BrandName: null,
                    InStock: ReadInStock(node),
                    // Markanın KENDİ sitesi.
                    Seller: null));
            }
        }

        return (result, filtered);
    }

    /// <summary>
    /// <c>outOfStock</c> alanı bool geliyor; sayı ya da metin gelirse de
    /// okunuyor. Alan hiç yoksa null ("bilmiyoruz") kalır — false ile
    /// karıştırılmamalı.
    /// </summary>
    private static bool? ReadInStock(JsonElement node)
    {
        if (!node.TryGetProperty("outOfStock", out var v))
            return null;

        return v.ValueKind switch
        {
            JsonValueKind.True => false,
            JsonValueKind.False => true,
            JsonValueKind.Number => v.GetInt32() == 0,
            JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? !b : null,
            _ => null,
        };
    }

    /// <summary>
    /// "1.145,00 ₺" -> 1145.00. Boş/biçimsiz değerde false döner.
    ///
    /// <b>`numericPrice` KULLANILMIYOR.</b> Kaynak o alanı float artığıyla
    /// veriyor: "Hair Plus" için 1144,9999999999997726263245568,
    /// "Kids Sleepy Bear" için 898,9999999999998863131622784. Görünen fiyat
    /// (`newPrice`) doğru, o yüzden Türkçe biçimli metin ayrıştırılıyor.
    /// </summary>
    private static bool TryParsePrice(string? raw, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // TurkishPriceParser yalnızca "TL" ekini atıyor; bu kaynak "₺"
        // simgesini kullanıyor.
        var text = raw.Replace("₺", "").Trim();
        if (text.Length == 0)
            return false;

        try
        {
            price = TurkishPriceParser.Parse(text);
        }
        catch (FormatException)
        {
            return false;
        }

        return price > 0;
    }

    /// <summary>
    /// Görsel yolunda BOŞLUK ve Türkçe harf var
    /// ("/media/uploads/images/Hair Plus Ayıcık 1709….png"), ham hâliyle
    /// istenirse adres geçersiz. <c>Uri</c> kurucusu yüzdeli kodlamayı
    /// kendisi yapıyor; kodlanmış adres 3 Eylül'de 200/image-png verdi.
    /// </summary>
    private static string? BuildImageUrl(string baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Uri.TryCreate(new Uri(baseUrl), path, out var uri) ? uri.AbsoluteUri : null;
    }

    private static string? Text(JsonElement node, string property) =>
        node.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()?.Trim()
            : null;

    /// <summary>
    /// Katalogdaki iki aksesuar: "Vita Bear Bamboo Saç Fırçası" (499 TL) ve
    /// "Vita Bear Sleepy Bear Uyku Bandı" (50 TL).
    ///
    /// <b>ORTAK SÜZGECE EKLENMEDİ, bilinçli.</b> Ortak
    /// <c>NonSupplementProductFilter</c> 2600+ ürünün tamamına uygulanıyor ve
    /// bu depoda geniş kalıplar iki kez gerçek ürün eledi ("canta[a-z]*"
    /// bir whey proteinini "Cantaloupe" yüzünden atıyordu). Bu iki kelime
    /// yalnızca bu kaynakta görüldüğü için kapsam burada tutuluyor; başka
    /// bir kaynakta da çıkarsa ortak listeye taşınmalı.
    ///
    /// Ortak süzgeç yine de çağrılıyor — shaker/tişört gibi bilinen
    /// aksesuarlar buraya eklenirse yakalansın diye.
    /// </summary>
    private static bool IsAccessory(string name) =>
        LocalAccessoryRegex().IsMatch(name) || NonSupplementProductFilter.IsAccessoryOrApparel(name);

    // Türkçe tuzağı: "Fırçası" ekli geliyor, `\b` eki kesmez — sonek serbest.
    // Ayrıca noktalı/noktasız i çifti açık harf sınıfıyla yazıldı: invariant
    // IgnoreCase "I" ile "ı"yı EŞLEŞTİRMEZ, yani "FIRÇASI" yazımı `fırça`
    // kalıbına takılmazdı.
    [GeneratedRegex(@"f[ıiİI]r[çc]a[a-zçğıöşü]*|uyku band[a-zçğıöşü]*", RegexOptions.IgnoreCase)]
    private static partial Regex LocalAccessoryRegex();
}

using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.PrimeNutrition;

/// <summary>
/// primenutrition.com.tr — OpenCart altyapılı, kendi sitesinden satan tek
/// markalı kaynak (on üçüncü kaynak).
///
/// <b>NEDEN schema.org KULLANILMIYOR:</b> sayfada bir Product şeması var ama
/// FİYATI BOZUK. Site, makine alanına Türkçe biçimli sayı yazıyor:
/// gerçek fiyat "1.299,00 TL" iken şemada <c>"price": "1.3"</c> görünüyor —
/// binlik ayracı olan nokta ondalık nokta sanılıyor. 1000 TL üstündeki her
/// ürün bu yüzden yanlış. Aynı blokta ürün ADI da kısaltılmış geliyor
/// ("Prime Nutrition Whey Protein"), öyle ki 67 ürünün 49'u adını başka bir
/// ürünle paylaşıyordu; sayfanın kendi &lt;h1&gt;/og:title'ı ise tam
/// ("... Whey Protein 495 gram Strawberry Cream"). İki sorunun da kök sebebi
/// aynı: şema bloğu güvenilmez, gerçek veri HTML'de.
///
/// Bu yüzden ad og:title'dan, fiyat sayfadaki fiyat kutusundan okunuyor.
/// Şemadan alınan tek şey stok durumu (enum, sayı biçiminden etkilenmiyor).
///
/// <b>ÜRÜN BAŞINA BİR İSTEK:</b> sitemap ürün ve kategori adreslerini bir
/// arada veriyor (115 adres), ayırt edici sayfadaki fiyat kutusu. ~75 ürün
/// × ~1 sn ≈ bir dakika; 6 saatlik turun (99 sn) üstüne binse bile pencerenin
/// %1'ini geçmiyor, o yüzden protein7/Provitamin gibi <c>DailyOnly</c>
/// işaretlenmedi.
/// </summary>
public partial class PrimeNutritionScraper(HttpClient httpClient, ILogger<PrimeNutritionScraper> logger) : IBrandScraper
{
    public string BrandName => "Prime Nutrition";
    public string BaseUrl => "https://www.primenutrition.com.tr";

    private const string SitemapUrl = "https://www.primenutrition.com.tr/sitemap.xml";

    // Nezaket beklemesi — karşı taraf küçük bir mağaza. protein7'de 0,3 sn
    // sorunsuz geçmişti; burada ürün sayısı çok daha az olduğu için yarım
    // saniye tutmanın maliyeti yok.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(500);

    // GERÇEK hataların oranı bunu aşarsa tarama güvenilmez sayılıyor.
    // Ürün olmayan adresler (kategori/blog/sözleşme sayfaları) buna DAHİL
    // DEĞİL — sitemap onları da listeliyor, beklenen durum.
    private const double MaxFailureRatio = 0.2;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var urls = await FetchUrlsAsync(cancellationToken);
        var result = new List<ScrapedProduct>();
        var failures = 0;
        var notProduct = 0;
        var accessory = 0;
        var noPrice = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (product, reason) = await FetchProductAsync(url, cancellationToken);
                if (product is not null)
                    result.Add(product);
                else if (reason == SkipReason.NotAProductPage)
                    notProduct++;
                else if (reason == SkipReason.Accessory)
                    accessory++;
                else
                    noPrice++;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Tek sayfa yüzünden tarama düşmemeli ama SAYILIYOR — sessiz
                // veri kaybı gürültülü hatadan tehlikelidir.
                failures++;
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        if (urls.Count > 0 && failures > urls.Count * MaxFailureRatio)
        {
            throw new InvalidOperationException(
                $"Prime Nutrition: {urls.Count} adresin {failures} tanesinde beklenmeyen hata oluştu, " +
                "tarama güvenilir değil.");
        }

        logger.LogInformation(
            "Prime Nutrition: {Total} adres tarandı, {Found} ürün alındı, {NotProduct} adres ürün sayfası değil, " +
            "{Accessory} aksesuar süzüldü, {NoPrice} üründe fiyat yok (tükenmiş), {Failures} hata.",
            urls.Count, result.Count, notProduct, accessory, noPrice, failures);

        return result;
    }

    private enum SkipReason
    {
        None,
        /// <summary>Kategori/blog/sözleşme sayfası — sitemap bunları da listeliyor.</summary>
        NotAProductPage,
        /// <summary>Takviye değil — havlu, shaker, anahtarlık, şapka.</summary>
        Accessory,
        /// <summary>
        /// Sayfada fiyat yok. Ölçüldü (2 Eylül): site TÜKENMİŞ ürünlerde
        /// fiyat yayınlamıyor, kutu boş geliyor. Fiyatsız ürünü almıyoruz —
        /// uydurma fiyat üretmek yasak; stok geri geldiğinde kendiliğinden
        /// katalogda yerini alıyor.
        /// </summary>
        NoPrice,
    }

    private async Task<(ScrapedProduct? Product, SkipReason Reason)> FetchProductAsync(
        string url, CancellationToken cancellationToken)
    {
        var html = await httpClient.GetStringAsync(url, cancellationToken);

        // Ürün sayfasını kategori/blog sayfasından ayıran şey "price_pr"
        // dizesinin sayfada GEÇMESİ. Ölçüldü (2 Eylül): kategori sayfasında
        // hiç geçmiyor; ürün sayfasında ya gerçek kutunun class'ı olarak ya
        // da (tükenmiş üründe) varyant JavaScript'inin seçicisi olarak var.
        if (!html.Contains("price_pr", StringComparison.OrdinalIgnoreCase))
            return (null, SkipReason.NotAProductPage);

        var name = ExtractName(html);
        if (name.Length == 0)
            return (null, SkipReason.NotAProductPage);

        // Katalogda havlu, shaker ve anahtarlık var ("prime-aksesuar"
        // kategorisi) — ortak süzgeç bunları zaten tanıyor.
        if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
            return (null, SkipReason.Accessory);

        // Fiyat kutusu boşsa ürün TÜKENMİŞ demektir — site o durumda fiyat
        // yayınlamıyor. Uydurma fiyat üretmiyoruz; stok geri geldiğinde ürün
        // kendiliğinden katalogda yerini alıyor.
        var price = ExtractPrice(html);
        if (price is null or <= 0)
            return (null, SkipReason.NoPrice);

        // Görsel adresinde de varlık geçebiliyor (sorgu dizesindeki "&amp;").
        var image = System.Net.WebUtility.HtmlDecode(
            OgImageRegex().Match(html).Groups[1].Value).Trim();

        // Şemanın FİYATI bozuk ama stok alanı bir enum, sayı biçiminden
        // etkilenmiyor.
        //
        // Pratikte buraya gelen ürünlerin hepsi stokta: site TÜKENMİŞ ürünlerde
        // fiyat yayınlamıyor, o yüzden onlar yukarıda NoPrice ile zaten
        // eleniyor (2 Eylül'de üç Optimus Pre-Workout varyantı böyleydi —
        // sayfalarında "Tükendi" yazıyor ve fiyat kutusu boş). Yine de
        // tanımadığımız her değerde null bırakılıyor ("bilmiyoruz"), false ile
        // karıştırılmıyor.
        var availability = AvailabilityRegex().Match(html).Groups[1].Value;
        bool? inStock = availability.Contains("InStock", StringComparison.OrdinalIgnoreCase) ? true
            : availability.Contains("OutOfStock", StringComparison.OrdinalIgnoreCase) ? false
            : null;

        return (new ScrapedProduct(
            Name: name,
            Url: url,
            ImageUrl: image.Length > 0 ? image : null,
            // Sitenin kendi kategorileri ("Amino Asit", "Performans & Güç")
            // bizim slug'larımıza birebir oturmuyor; isimden çıkarım diğer
            // markalarla tutarlı sonuç veriyor.
            Category: null,
            Price: price.Value,
            InStock: inStock), SkipReason.None);
    }

    /// <summary>
    /// Ürün adı. Sayfanın og:title'ından okunuyor — sitenin schema.org
    /// bloğundaki ad KISALTILMIŞ geliyor ("Prime Nutrition Whey Protein"),
    /// öyle ki 67 ürünün 49'u adını başka bir ürünle paylaşıyordu. og:title
    /// tam adı veriyor ("... Whey Protein 495 gram Strawberry Cream") ve ilk
    /// gerçek taramada 57 ürünün 57'si benzersiz çıktı.
    ///
    /// HTML varlıkları ÇÖZÜLÜYOR: "Cookie &amp;amp; Cream" gibi adlar
    /// geliyor, çözülmezse kullanıcıya aynen öyle görünüyor (ilk taramada
    /// 57 ürünün 12'si etkilenmişti). BigJoy/HIQ/Hardline de aynısını yapıyor.
    /// </summary>
    internal static string ExtractName(string html) =>
        System.Net.WebUtility.HtmlDecode(OgTitleRegex().Match(html).Groups[1].Value).Trim();

    /// <summary>
    /// Sayfadaki fiyat kutusundan ürünün fiyatını okur; kutu yoksa
    /// <c>null</c> döner (kategori/blog sayfası).
    ///
    /// DİKKAT — kutuda İKİ tutar var: önce normal fiyat, sonra "Havale / EFT"
    /// indirimli tutarı (1.299,00 → 1.234,05). Kartla ödeyen normal fiyatı
    /// ödüyor, o yüzden İLKİ alınıyor. <c>TurkishPriceParser.ParsePricePair</c>
    /// burada KULLANILAMAZ: o metindeki SON tutarı güncel fiyat sayar ve
    /// havale fiyatını seçerdi — yani her ürünü %5 ucuz gösterirdik.
    /// </summary>
    internal static decimal? ExtractPrice(string html)
    {
        var match = PriceBoxRegex().Match(html);
        if (!match.Success)
            return null;

        var text = match.Groups["price"].Value;
        return text.Length == 0 ? null : TurkishPriceParser.Parse(text);
    }

    private async Task<List<string>> FetchUrlsAsync(CancellationToken cancellationToken)
    {
        var xml = await httpClient.GetStringAsync(SitemapUrl, cancellationToken);
        return LocRegex().Matches(xml)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(u => u.Length > 0)
            .Distinct()
            .ToList();
    }

    [GeneratedRegex(@"<loc>([^<]+)</loc>", RegexOptions.IgnoreCase)]
    private static partial Regex LocRegex();

    [GeneratedRegex(@"<meta\s+property=""og:title""\s+content=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitleRegex();

    [GeneratedRegex(@"<meta\s+property=""og:image""\s+content=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgImageRegex();

    // Fiyat kutusundan SONRAKİ İLK tutar.
    //
    // "price_pr" dizesi sayfada İKİ ANLAMDA geçiyor: gerçek kutunun class'ı
    // (<ul class="list-unstyled price_pr col-6">) ve varyant değiştiren
    // JavaScript içinde bir seçici ($('#price_pr').html(...)). Tükenmiş
    // ürünlerde yalnızca JS'teki geçiyor. Sadece "price_pr" aransaydı JS
    // bloğuna tutunup oradaki ürün dizisinden yanlış bir fiyat çekme riski
    // olurdu — bu yüzden kalıp CLASS ÖZNİTELİĞİNE bağlandı.
    //
    // Pencere dar (400 karakter): geniş olsaydı kutu boşken sayfanın
    // ilerisindeki başka bir ürünün fiyatını yakalardı.
    [GeneratedRegex(@"class=""[^""]*price_pr[^""]*"".{0,400}?(?<price>\d{1,3}(?:\.\d{3})*,\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceBoxRegex();

    [GeneratedRegex(@"""availability""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AvailabilityRegex();
}

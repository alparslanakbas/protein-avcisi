using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.ProteinPazari;

/// <summary>
/// proteinpazari.com.tr — otuz altıncı kaynak, ALTINCI BAYİ. OpenCart.
///
/// <b>ÜRÜN BAŞINA İSTEK YOK.</b> Kategori sayfasındaki ürün kartı ihtiyacımız
/// olan HER ŞEYİ taşıyor: ad, adres, görsel, fiyat, mağaza eski fiyatı, stok
/// durumu ve <b>marka</b>. Katalogun tamamı 49 istekte iniyor (11 üst + 38 alt
/// kategori, sayfalama dahil). Bu yüzden 792 ürünlük büyüklüğüne rağmen
/// <c>DailyOnly</c> DEĞİL — protein7 (~900) ve Provitamin (~430) ürün başına
/// istek attığı için gecelik tura alınmıştı, burada o maliyet yok.
///
/// <b>SITEMAP KULLANILMIYOR — çünkü yok.</b> Üç uç da denendi:
/// <c>/sitemap</c> 404, <c>extension/feed/google_sitemap</c> ve
/// <c>feed/google_sitemap</c> 200 dönüp BOŞ gövde veriyor. Kategori listesi
/// sitenin kendi HTML sitemap sayfasından okunuyor
/// (<c>index.php?route=information/sitemap</c>), alt kategoriler de üst
/// kategori sayfalarından toplanıyor.
///
/// <b>MARKA SAYFADAN OKUNUYOR, isimden tahmin EDİLMİYOR.</b> Kart üzerinde
/// "Marka: X" satırı var. Adlar büyük harfli ve Türkçe noktalı İ taşıyor
/// ("BİG JOY SPORTS", "PRİME NUTRİTİON"); <c>BrandNameNormalizer</c> sözlüğü
/// <c>OrdinalIgnoreCase</c> olduğu ve bu karşılaştırma Türkçe İ'yi katlamadığı
/// için o yazımlar takma ad listesine TEK TEK eklendi — eklenmeseydi katalogda
/// "BigJoy" varken ikinci bir "BİG JOY SPORTS" markası oluşurdu.
/// Kartında marka satırı hiç olmayan ürünlerde (7 Ekim: 54) marka adın başından
/// okunuyor, ama yalnızca bu taramanın kartlarında görülen marka adlarıyla
/// birebir kelime eşleşmesi varsa (bkz. <see cref="MarkayiAdindanBul"/>).
///
/// <b>AKSESUAR İKİ SÜZGEÇTEN GEÇİYOR, ikisi de gerekli (ölçüldü, 4 Eylül):</b>
/// <list type="number">
/// <item>Kaynağın kendi <c>fitness-aksesuar</c> kategorisi (alt kategorileriyle
/// birlikte 129 ürün) tamamen atlanıyor. Bunların 16'sını ad süzgeci
/// YAKALAMIYOR — dizlik, dirseklik, knee wraps, matara, water bottle,
/// grip pad gibi kelimeler listede yok.</item>
/// <item>Ortak <c>NonSupplementProductFilter</c> yine de uygulanıyor: kategoriye
/// konmamış 2 ürünü (cajun baharatı, ketçap) o yakalıyor.</item>
/// </list>
/// Sonuç: 923 ham kayıt → 792 ürün.
/// </summary>
public sealed partial class ProteinPazariScraper(HttpClient httpClient, ILogger<ProteinPazariScraper> logger)
    : IBrandScraper
{
    // Ürünün markası ne kartta ne adında bulunamazsa kullanılacak ad (bkz.
    // MarkayiAdindanBul). 7 Ekim'de 54 ürün buradaydı, eşleştirmeden sonra 2.
    public string BrandName => "Protein Pazarı";
    public string BaseUrl => "https://proteinpazari.com.tr";

    private const string SellerName = "proteinpazari.com.tr";

    /// <summary>Kategori bağlantılarını taşıyan HTML sitemap sayfası.</summary>
    private const string SitemapPageUrl = "index.php?route=information/sitemap";

    /// <summary>
    /// Kaynağın aksesuar/giyim kategorisi — alt ağacıyla birlikte hiç
    /// gezilmiyor. Kara liste yerine kategoriyle atlamanın sebebi yukarıda.
    /// </summary>
    private const string AccessoryCategorySlug = "fitness-aksesuar";

    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(400);

    private const int MaxPagesPerCategory = 15;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var categories = await CollectCategoriesAsync(cancellationToken);
        if (categories.Count == 0)
            throw new InvalidOperationException("proteinpazari: hiç kategori bağlantısı bulunamadı.");

        var products = new Dictionary<string, ScrapedProduct>(StringComparer.OrdinalIgnoreCase);
        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = 0;

        foreach (var category in categories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var page = 1; page <= MaxPagesPerCategory; page++)
            {
                string html;
                try
                {
                    html = await httpClient.GetStringAsync($"{category}?limit=100&page={page}", cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    failures++;
                    break;
                }
                finally
                {
                    await Task.Delay(DelayBetweenRequests, cancellationToken);
                }

                var cards = ParseCards(html);
                if (cards.Count == 0)
                    break;

                foreach (var (url, product) in cards)
                {
                    if (product is null)
                        filtered.Add(url);
                    else
                        products.TryAdd(url, product);
                }

                // Sayfa dolmadıysa son sayfadayız.
                if (cards.Count < 100)
                    break;
            }
        }

        if (products.Count == 0)
            throw new InvalidOperationException("proteinpazari: hiç ürün alınamadı.");

        // Kartında marka olmayan ürün: marka adının başından, yalnız bu taramada başka kartlarda görülen adlarla.
        var markalar = products.Values.Select(p => p.BrandName).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var markasiz = 0;
        var adindan = 0;
        foreach (var (url, urun) in products.ToList())
        {
            if (urun.BrandName is not null)
                continue;
            markasiz++;
            if (MarkayiAdindanBul(urun.Name, markalar) is { } marka)
            {
                products[url] = urun with { BrandName = marka };
                adindan++;
            }
        }

        logger.LogInformation(
            "proteinpazari: {Categories} kategori gezildi, {Found} ürün alındı, {Filtered} takviye dışı süzüldü, {Failures} sayfa hatası; markasız {Markasiz} kartın {Adindan}'i markayı adından aldı.",
            categories.Count, products.Count, filtered.Count, failures, markasiz, adindan);

        return [.. products.Values];
    }

    /// <summary>
    /// Gezilecek kategori adreslerini toplar: HTML sitemap'teki kök seviye
    /// bağlantılar + her birinin alt kategorileri.
    ///
    /// Sitemap sayfasında bilgi sayfaları da (kargo, KVKK, banka bilgileri)
    /// aynı biçimde duruyor. Bunlar İSİMLE ELENMİYOR — o liste bakımsız kalır
    /// ve site yeni bir bilgi sayfası eklediğinde sessizce kirlenir. Onun
    /// yerine sayfa gezildiğinde ürün kartı çıkmıyorsa kendiliğinden düşüyor;
    /// maliyeti dokuz boş istek.
    /// </summary>
    internal static List<string> ParseCategoryLinks(string html, string baseUrl) =>
        RootLevelLinkRegex().Matches(html)
            .Select(m => m.Groups[1].Value.TrimEnd('/'))
            .Where(u => u.Length > baseUrl.Length)
            .Where(u => !u.Contains($"/{AccessoryCategorySlug}", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<List<string>> CollectCategoriesAsync(CancellationToken cancellationToken)
    {
        var sitemap = await httpClient.GetStringAsync(SitemapPageUrl, cancellationToken);
        var top = ParseCategoryLinks(sitemap, BaseUrl);

        var all = new List<string>(top);
        var seen = new HashSet<string>(top, StringComparer.OrdinalIgnoreCase);

        foreach (var category in top)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync($"{category}?limit=100", cancellationToken);
                foreach (var sub in ParseCategoryLinks(html, BaseUrl))
                {
                    // Yalnızca bu kategorinin altındakiler; sayfalama ve
                    // sıralama bağlantıları (".../limit-100", ".../page-2")
                    // kategori değil.
                    if (!sub.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (PagingSegmentRegex().IsMatch(sub))
                        continue;
                    if (seen.Add(sub))
                        all.Add(sub);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Tek bir kategorinin düşmesi taramayı bitirmemeli.
            }

            await Task.Delay(DelayBetweenRequests, cancellationToken);
        }

        return all;
    }

    /// <summary>
    /// Liste sayfasındaki ürün kartlarını ayrıştırır. Değer <c>null</c> ise
    /// kart okunmuş ama takviye dışı olduğu için elenmiştir.
    ///
    /// <b>ÖNCE STİL VE BETİK BLOKLARI ATILIYOR.</b> Sayfa satır içi CSS
    /// taşıyor ve o CSS "product-thumb" ifadesini 400'den fazla kez
    /// geçiriyor; ham HTML üzerinde kart aramak yüzlerce sahte eşleşme
    /// üretiyordu (ölçüm sırasında tam bu oldu).
    /// </summary>
    internal static List<(string Url, ScrapedProduct? Product)> ParseCards(string html)
    {
        var body = ScriptOrStyleRegex().Replace(html, string.Empty);
        var result = new List<(string, ScrapedProduct?)>();

        foreach (Match card in ProductCardRegex().Matches(body))
        {
            var block = card.Value;

            var nameMatch = CardNameRegex().Match(block);
            if (!nameMatch.Success)
                continue;

            var url = WebUtility.HtmlDecode(nameMatch.Groups[1].Value).Trim();
            var name = WebUtility.HtmlDecode(nameMatch.Groups[2].Value).Trim();
            if (url.Length == 0 || name.Length == 0)
                continue;

            if (NonSupplementProductFilter.IsAccessoryOrApparel(name))
            {
                result.Add((url, null));
                continue;
            }

            // İndirimli üründe "price-new"/"price-old", normalde "price-normal".
            var priceText = CardNewPriceRegex().Match(block) is { Success: true } n
                ? n.Groups[1].Value
                : CardNormalPriceRegex().Match(block) is { Success: true } p ? p.Groups[1].Value : null;

            var price = ParsePrice(priceText);
            if (price is null or <= 0m)
                continue;

            var oldPrice = ParsePrice(CardOldPriceRegex().Match(block) is { Success: true } o
                ? o.Groups[1].Value
                : null);

            string? brand = null;
            var brandMatch = CardBrandRegex().Match(block);
            if (brandMatch.Success)
            {
                var raw = WebUtility.HtmlDecode(brandMatch.Groups[1].Value).Trim();
                if (raw.Length > 0)
                    brand = BrandNameNormalizer.Normalize(raw);
            }

            var image = CardImageRegex().Match(block) is { Success: true } i
                ? WebUtility.HtmlDecode(i.Groups[1].Value).Trim()
                : null;

            result.Add((url, new ScrapedProduct(
                Name: name,
                Url: url,
                ImageUrl: string.IsNullOrWhiteSpace(image) ? null : image,
                // Kaynağın kategorileri bizim slug'larımıza oturmuyor;
                // kategori ürün adından çıkarılıyor.
                Category: null,
                Price: price.Value,
                StoreOldPrice: oldPrice > price ? oldPrice : null,
                BrandName: brand,
                // Kart "out-of-stock" sınıfı taşıyorsa ürün tükenmiş.
                // Kaynak bunu her üründe veriyor, o yüzden null bırakılmıyor.
                InStock: !OutOfStockRegex().IsMatch(block),
                Seller: SellerName)));
        }

        return result;
    }

    /// <summary>
    /// Kartında marka olmayan ürünün markasını adının başından bulur; yalnızca <paramref name="markalar"/> içinden
    /// (aynı taramada başka kartların "Marka:" satırında görülen adlar). Bulamazsa null.
    /// </summary>
    /// <remarks>
    /// <b>Neden (7 Ekim):</b> 54 ürünün kartında "stats" bloğu hiç yok, kaynak bu ürünlere marka atamamış (ürün
    /// sayfasında da yok). Marka bilinmeyince kazıyıcının kendi adı ("Protein Pazarı") yazılıyordu: bayi adıyla sahte
    /// bir marka sayfası oluşmuştu ve "her markadan bir ürün" listelerinde aynı Kingsize kreatini iki sırada çıkıyordu.
    /// Ama marka adın içinde açıkça yazıyor ("KİNGSİZE NUTRİTİON CREATINE POWDER…").
    /// <b>Neden yalnızca bu taramanın marka adları:</b> bu bayinin gerçekten taşıdığı markalar ve yutma servisinde
    /// zaten mevcut kayıtlara çözülüyorlar; serbest bir ad kopya marka yaratırdı (bkz. ResolveBrand). Ad tahmini YOK:
    /// adın ilk kelimeleri bir marka adının kelimeleriyle birebir aynı olmalı ("BİG JOY" = "BigJoy" gibi bitişik
    /// yazım da sayılıyor); en uzun eşleşme kazanıyor, bu yüzden "PRIME HYDRATION" Prime Nutrition'a değil Prime
    /// Hydration'a gidiyor. 54 gerçek adın 52'si eşleşti; eşleşmeyen ikisinin (Danvita, Flava) markası bu bayinin
    /// hiçbir kartında yok ve boş kalıyor.
    /// </remarks>
    internal static string? MarkayiAdindanBul(string ad, IReadOnlyCollection<string> markalar)
    {
        var adParcalari = Kelimeler(ad);
        string? bulunan = null;
        var bulunanUzunluk = 0;

        foreach (var marka in markalar)
        {
            var markaParcalari = Kelimeler(marka);
            if (markaParcalari.Count == 0)
                continue;

            var tuketilen = OnekEslesmesi(adParcalari, markaParcalari);
            if (tuketilen == 0)
                continue;

            // Eşit uzunlukta iki eşleşme aynı ilk kelimelere dayandığı için aynı markadır; çakışma olamıyor.
            var uzunluk = adParcalari.Take(tuketilen).Sum(k => k.Length);
            if (uzunluk > bulunanUzunluk)
                (bulunan, bulunanUzunluk) = (marka, uzunluk);
        }

        return bulunan;
    }

    // Kaç ad kelimesinin markaya karşılık geldiği; eşleşme yoksa 0. Kelime kelime ya da bitişik yazım (en çok 3 kelime).
    private static int OnekEslesmesi(List<string> ad, List<string> marka)
    {
        if (marka.Count <= ad.Count && ad.Take(marka.Count).SequenceEqual(marka, StringComparer.Ordinal))
            return marka.Count;

        var bitisik = string.Concat(marka);
        for (var j = 1; j <= Math.Min(3, ad.Count); j++)
        {
            if (string.Equals(string.Concat(ad.Take(j)), bitisik, StringComparison.Ordinal))
                return j;
        }

        return 0;
    }

    // Türkçe harfler katlanmış, küçük harf kelimeler; tire kelimenin parçası ("Z-Konzept"), diğer işaretler ayırıcı
    // ("Bite & More" = "BİTE MORE").
    private static List<string> Kelimeler(string metin)
    {
        var katli = new System.Text.StringBuilder(metin.Length);
        foreach (var ch in metin)
        {
            katli.Append(ch switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' or 'I' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(ch),
            });
        }

        return KelimeAyiriciRegex().Split(katli.ToString()).Where(k => k.Length > 0).ToList();
    }

    [GeneratedRegex(@"[^\p{L}\p{N}-]+")]
    private static partial Regex KelimeAyiriciRegex();

    /// <summary>
    /// "1.299,00TL" → 1299.00. Fiyat Türkçe biçimde yazılıyor; invariant
    /// kültürle okunsaydı 1.299 olurdu (bu tuzağa projede daha önce düşüldü).
    /// </summary>
    internal static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = PriceCleanupRegex().Replace(WebUtility.HtmlDecode(text), string.Empty).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, TurkishCulture, out var value) ? value : null;
    }

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleRegex();

    [GeneratedRegex(@"href=""(https://proteinpazari\.com\.tr/[a-z0-9/-]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex RootLevelLinkRegex();

    [GeneratedRegex(@"/(page|limit|sort)-", RegexOptions.IgnoreCase)]
    private static partial Regex PagingSegmentRegex();

    // Bir kart, bir sonraki kart ya da sayfalama bloğu başlayana kadar sürer.
    [GeneratedRegex(@"<div class=""product-layout[^""]*"">.*?(?=<div class=""product-layout|<div class=""pagination|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ProductCardRegex();

    [GeneratedRegex(@"<div class=""name""><a href=""([^""]+)""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex CardNameRegex();

    [GeneratedRegex(@"stats-label"">Marka:</span>\s*<span><a[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex CardBrandRegex();

    [GeneratedRegex(@"class=""price-normal"">([^<]+)</span>", RegexOptions.IgnoreCase)]
    private static partial Regex CardNormalPriceRegex();

    [GeneratedRegex(@"class=""price-new"">([^<]+)</span>", RegexOptions.IgnoreCase)]
    private static partial Regex CardNewPriceRegex();

    [GeneratedRegex(@"class=""price-old"">([^<]+)</span>", RegexOptions.IgnoreCase)]
    private static partial Regex CardOldPriceRegex();

    [GeneratedRegex(@"data-src=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CardImageRegex();

    [GeneratedRegex(@"<div class=""product-layout[^""]*\bout-of-stock\b", RegexOptions.IgnoreCase)]
    private static partial Regex OutOfStockRegex();

    // Para birimi, boşluk ve sıfır genişlikli karakterler.
    [GeneratedRegex(@"[^\d.,]", RegexOptions.IgnoreCase)]
    private static partial Regex PriceCleanupRegex();
}

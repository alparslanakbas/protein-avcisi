using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Bir robots.txt'nin bizim botumuza uygulanan kuralları (RFC 9309). WheyProof'taki RobotsRules'un aynısı.
/// </summary>
/// <remarks>
/// <b>NEDEN (8 Ekim 2026).</b> Shopify'ın bot kaydı, botun her mağazanın robots.txt'sine uyduğunu
/// onaylatıyor; iki site Shopify'a tek bot olarak (wheyproofbot) kayıtlı, TR'nin 7 Shopify mağazası da
/// başvuruda yazıyor. O gün ölçüldü: iki sitenin 61 Shopify adresinin hepsi kullandığımız üç yola
/// (katalog, ana sayfa, ürün sayfası) izin veriyor, hiçbiri crawl-delay istemiyor; yani bugün bir şey
/// değişmiyor, bu kod beyanı ileride de doğru tutuyor.
///
/// <b>Kurallar</b> (RFC 9309, Google'ınkiyle aynı): ürün adımızı anan grup (büyük/küçük harf fark
/// etmez), yoksa <c>*</c> grubu; birden çok eşleşen grup birleşir. En uzun eşleşen desen kazanır,
/// eşitlikte izin kazanır. <c>*</c> herhangi bir diziyi, sondaki <c>$</c> sonu sabitler. Desenler
/// sorgusuyla birlikte yola karşı denenir.
///
/// <b>Crawl-delay (3 Kasım 2026).</b> RFC'de yok ama Shopify'ın bot incelemesi buna nasıl uyduğumuzu
/// sordu; o güne kadar okunup geçiliyordu. Artık bizim grubumuzdaki (yoksa <c>*</c> grubundaki) saniye,
/// aynı adrese iki istek arasındaki en kısa süre (bkz. <see cref="RobotsTxtOnbellegi.AralikBekleAsync"/>);
/// birden çok grup eşleşirse en uzunu. Pozitif sayı olmayan değer yok sayılır.
/// </remarks>
public sealed class RobotsKurallari
{
    private readonly List<(bool Izin, string Desen, Regex Ifade)> kurallar;

    private RobotsKurallari(List<(bool, string, Regex)> kurallar, TimeSpan? taramaGecikmesi)
    {
        this.kurallar = kurallar;
        TaramaGecikmesi = taramaGecikmesi;
    }

    /// <summary>Kural yok: her şey serbest (robots.txt bulunmadığında da anlamı bu, RFC 9309 2.3.1.3).</summary>
    public static readonly RobotsKurallari HepsiSerbest = new([], null);

    /// <summary>robots.txt bize bir Crawl-delay veriyorsa aynı adrese iki istek arasındaki en kısa süre.</summary>
    public TimeSpan? TaramaGecikmesi { get; }

    private sealed class Grup
    {
        public List<string> Ajanlar { get; } = [];
        public List<(bool Izin, string Desen)> Kurallar { get; } = [];
        public double? GecikmeSaniye { get; set; }
    }

    public static RobotsKurallari Ayristir(string metin, string urunAdi)
    {
        var gruplar = new List<Grup>();
        Grup? gecerli = null;
        var oncekiAjan = false;

        foreach (var ham in metin.Split('\n'))
        {
            var satir = ham.Split('#', 2)[0].Trim();
            var iki = satir.IndexOf(':');
            if (iki <= 0)
                continue;

            var ad = satir[..iki].Trim().ToLowerInvariant();
            var deger = satir[(iki + 1)..].Trim();
            if (ad == "user-agent")
            {
                if (!oncekiAjan || gecerli is null)
                {
                    gecerli = new Grup();
                    gruplar.Add(gecerli);
                }
                gecerli.Ajanlar.Add(deger);
                oncekiAjan = true;
            }
            else if (ad is "allow" or "disallow")
            {
                oncekiAjan = false;
                // Ajan satırından önceki kurallar hiçbir gruba ait değil; boş disallow her şeye izin verir.
                if (gecerli is not null && deger.Length > 0)
                    gecerli.Kurallar.Add((ad == "allow", deger));
            }
            else if (ad == "crawl-delay")
            {
                oncekiAjan = false;
                if (gecerli is not null
                    && double.TryParse(deger, NumberStyles.Float, CultureInfo.InvariantCulture, out var saniye)
                    && saniye > 0 && double.IsFinite(saniye))
                    gecerli.GecikmeSaniye = Math.Max(gecerli.GecikmeSaniye ?? 0, saniye);
            }
            else
            {
                oncekiAjan = false;
            }
        }

        var bizim = gruplar.Where(g => g.Ajanlar.Any(a => a.Equals(urunAdi, StringComparison.OrdinalIgnoreCase))).ToList();
        if (bizim.Count == 0)
            bizim = gruplar.Where(g => g.Ajanlar.Contains("*")).ToList();

        var gecikme = bizim.Max(g => g.GecikmeSaniye);
        return new RobotsKurallari(bizim.SelectMany(g => g.Kurallar)
            .Select(k => (k.Izin, k.Desen, IfadeyeCevir(k.Desen)))
            // Bir günle sınır yalnız TimeSpan taşmasın diye; o kadar uzun gecikme adresi zaten okutmaz.
            .ToList(), gecikme is { } s ? TimeSpan.FromSeconds(Math.Min(s, TimeSpan.FromDays(1).TotalSeconds)) : null);
    }

    /// <summary><paramref name="yolVeSorgu"/> istenebilir mi.</summary>
    public bool IzinVerir(string yolVeSorgu)
    {
        if (yolVeSorgu.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase))
            return true;

        var enIyi = kurallar
            .Where(k => k.Ifade.IsMatch(yolVeSorgu))
            .OrderByDescending(k => k.Desen.Length)
            .ThenByDescending(k => k.Izin)
            .FirstOrDefault();
        return enIyi.Desen is null || enIyi.Izin;
    }

    private static Regex IfadeyeCevir(string desen)
    {
        var sabit = desen.EndsWith('$');
        var govde = Regex.Escape(sabit ? desen[..^1] : desen).Replace(@"\*", ".*");
        return new Regex("^" + govde + (sabit ? "$" : ""), RegexOptions.CultureInvariant);
    }
}

/// <summary>
/// Her adresin robots.txt kuralları; günde en fazla bir kez okunur (tekil, istemciler paylaşır).
/// </summary>
/// <remarks>
/// Durumlar RFC 9309 2.3.1'e göre: 2xx ayrıştırılır; 429 dışındaki 4xx robots.txt yok demek, her şey
/// serbest; 429, 5xx ya da yanıt yok "ulaşılamıyor" demek ve son bilinen kurallar korunur. Hiç kural
/// okunmamışsa istek atlanır (RFC'nin tam yasağı) ve robots.txt her istekte değil bir saat sonra
/// yeniden sorulur.
/// </remarks>
public sealed class RobotsTxtOnbellegi(TimeProvider zaman)
{
    internal static readonly TimeSpan TazeSure = TimeSpan.FromHours(24);
    internal static readonly TimeSpan UlasilamazsaTekrar = TimeSpan.FromHours(1);

    /// <summary>
    /// İstekler arasında beklediğimiz en uzun Crawl-delay. Daha fazlasını isteyen adres hiç okunmuyor
    /// (turu saatlerce tutardı); bu da onu istenenden hızlı okumamak demek.
    /// </summary>
    internal static readonly TimeSpan EnUzunTaramaGecikmesi = TimeSpan.FromMinutes(5);

    private sealed record Kayit(RobotsKurallari? Kurallar, DateTimeOffset SonrakiOkuma);

    private readonly ConcurrentDictionary<string, Kayit> kayitlar = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> kapilar = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> sonIstekler = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> aralikKapilari = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="host"/> adresine son istekten (hangi istemciden olursa) bu yana
    /// <paramref name="gecikme"/> geçene kadar bekler, sonra bu isteği sayar. Gecikme
    /// <see cref="EnUzunTaramaGecikmesi"/>'nden uzunsa beklemeden false döner.
    /// </summary>
    public async Task<bool> AralikBekleAsync(string host, TimeSpan gecikme, CancellationToken cancellationToken)
    {
        if (gecikme > EnUzunTaramaGecikmesi)
            return false;

        var kapi = aralikKapilari.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
        await kapi.WaitAsync(cancellationToken);
        try
        {
            if (sonIstekler.TryGetValue(host, out var son))
            {
                var bekle = son + gecikme - zaman.GetUtcNow();
                if (bekle > TimeSpan.Zero)
                    await Task.Delay(bekle, zaman, cancellationToken);
            }
            sonIstekler[host] = zaman.GetUtcNow();
            return true;
        }
        finally
        {
            kapi.Release();
        }
    }

    /// <summary><paramref name="host"/> için kurallar; okunamadıysa null (istek atlanır).</summary>
    public async Task<RobotsKurallari?> GetirAsync(string host, string urunAdi,
        Func<CancellationToken, Task<HttpResponseMessage>> oku, ILogger logger, CancellationToken cancellationToken)
    {
        if (kayitlar.TryGetValue(host, out var bilinen) && zaman.GetUtcNow() < bilinen.SonrakiOkuma)
            return bilinen.Kurallar;

        var kapi = kapilar.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
        await kapi.WaitAsync(cancellationToken);
        try
        {
            if (kayitlar.TryGetValue(host, out bilinen) && zaman.GetUtcNow() < bilinen.SonrakiOkuma)
                return bilinen.Kurallar;

            var onceki = bilinen?.Kurallar;
            RobotsKurallari? kurallar;
            var ulasilamadi = false;
            try
            {
                using var yanit = await oku(cancellationToken);
                var durum = (int)yanit.StatusCode;
                if (yanit.IsSuccessStatusCode)
                    kurallar = RobotsKurallari.Ayristir(await yanit.Content.ReadAsStringAsync(cancellationToken), urunAdi);
                else if (durum is >= 400 and < 500 && yanit.StatusCode != HttpStatusCode.TooManyRequests)
                    kurallar = RobotsKurallari.HepsiSerbest;
                else
                {
                    ulasilamadi = true;
                    kurallar = onceki;
                    logger.LogWarning("{Host} robots.txt {Durum} döndü; {Ne}.", host, durum,
                        onceki is null ? "şimdilik isteklere gidilmiyor" : "son kurallar kullanılıyor");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                ulasilamadi = true;
                kurallar = onceki;
                logger.LogWarning(ex, "{Host} robots.txt okunamadı; {Ne}.", host,
                    onceki is null ? "şimdilik isteklere gidilmiyor" : "son kurallar kullanılıyor");
            }

            kayitlar[host] = new Kayit(kurallar, zaman.GetUtcNow() + (ulasilamadi ? UlasilamazsaTekrar : TazeSure));
            return kurallar;
        }
        finally
        {
            kapi.Release();
        }
    }
}

/// <summary>
/// İsteği yalnızca adresin robots.txt'si botumuza izin veriyorsa ve Crawl-delay'i dolduysa geçirir
/// (bkz. <see cref="RobotsKurallari"/>). İzin verilmeyen istek gönderilmez: nedenini söyleyen yerel bir
/// 403 döner; çekici bunu başarısız kaynak sayar ve mağaza okunmak yerine bayatlar.
/// </summary>
/// <remarks>
/// Shopify istemcilerinde en dışta durur; robots.txt'nin kendisi de zincirin geri kalanından geçerek,
/// yani botun kimliğiyle ve imzalı okunur.
/// </remarks>
public sealed class RobotsTxtIsleyicisi(RobotsTxtOnbellegi onbellek, ILogger<RobotsTxtIsleyicisi> logger,
    IReadOnlySet<string>? yalnizcaAdresler = null) : DelegatingHandler
{
    /// <summary>robots.txt dosyalarının bize seslendiği ad (WheyProof ile ortak bot).</summary>
    public const string UrunAdi = "wheyproofbot";

    internal const string RetGerekcesi = "Disallowed by robots.txt";
    internal const string GecikmeGerekcesi = "Crawl-delay of robots.txt is longer than we wait";

    /// <summary>
    /// TR'nin Shopify mağazaları (puan tazelemesi bunlarda robots'a bakar). Yeni bir Shopify mağazası
    /// eklendiğinde adresi buraya da yazılmalı; çekici istemcileri bu işleyiciyi zaten kendileri takıyor.
    /// </summary>
    public static readonly IReadOnlySet<string> ShopifyAdresleri = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "takehiq.com", "www.commandernutrition.com", "www.supraprotein.com", "supplementfactory.com.tr",
        "gigis.com.tr", "fellasfoods.com.tr", "www.bahsbar.com",
        // 5 Kasım 2026'da ikas'tan Shopify'a geçti.
        "kiperin.com",
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri is null || !uri.IsAbsoluteUri || uri.AbsolutePath.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)
            || (yalnizcaAdresler is not null && !yalnizcaAdresler.Contains(uri.Host)))
            return await base.SendAsync(request, cancellationToken);

        var kurallar = await onbellek.GetirAsync(uri.Host, UrunAdi, ct =>
        {
            var robots = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "/robots.txt"));
            foreach (var ajan in request.Headers.UserAgent)
                robots.Headers.UserAgent.Add(ajan);
            return base.SendAsync(robots, ct);
        }, logger, cancellationToken);

        if (kurallar is not null && kurallar.IzinVerir(uri.PathAndQuery))
        {
            if (kurallar.TaramaGecikmesi is { } gecikme && !await onbellek.AralikBekleAsync(uri.Host, gecikme, cancellationToken))
            {
                logger.LogWarning("{Host} robots.txt {Gecikme} Crawl-delay istiyor, beklediğimizden uzun; istenmedi.",
                    uri.Host, gecikme);
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { ReasonPhrase = GecikmeGerekcesi, RequestMessage = request };
            }
            return await base.SendAsync(request, cancellationToken);
        }

        if (kurallar is not null)
            logger.LogWarning("{Host} robots.txt {Yol} yoluna izin vermiyor; istenmedi.", uri.Host, uri.AbsolutePath);
        return new HttpResponseMessage(HttpStatusCode.Forbidden) { ReasonPhrase = RetGerekcesi, RequestMessage = request };
    }
}

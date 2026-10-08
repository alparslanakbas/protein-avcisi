using System.Collections.Concurrent;
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
/// </remarks>
public sealed class RobotsKurallari
{
    private readonly List<(bool Izin, string Desen, Regex Ifade)> kurallar;

    private RobotsKurallari(List<(bool, string, Regex)> kurallar) => this.kurallar = kurallar;

    /// <summary>Kural yok: her şey serbest (robots.txt bulunmadığında da anlamı bu, RFC 9309 2.3.1.3).</summary>
    public static readonly RobotsKurallari HepsiSerbest = new([]);

    public static RobotsKurallari Ayristir(string metin, string urunAdi)
    {
        var gruplar = new List<(List<string> Ajanlar, List<(bool Izin, string Desen)> Kurallar)>();
        List<string>? ajanlar = null;
        List<(bool, string)>? gecerli = null;
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
                if (!oncekiAjan || ajanlar is null)
                {
                    ajanlar = [];
                    gecerli = [];
                    gruplar.Add((ajanlar, gecerli));
                }
                ajanlar.Add(deger);
                oncekiAjan = true;
            }
            else if (ad is "allow" or "disallow")
            {
                oncekiAjan = false;
                // Ajan satırından önceki kurallar hiçbir gruba ait değil; boş disallow her şeye izin verir.
                if (gecerli is not null && deger.Length > 0)
                    gecerli.Add((ad == "allow", deger));
            }
            else
            {
                oncekiAjan = false;
            }
        }

        var bizim = gruplar.Where(g => g.Ajanlar.Any(a => a.Equals(urunAdi, StringComparison.OrdinalIgnoreCase))).ToList();
        if (bizim.Count == 0)
            bizim = gruplar.Where(g => g.Ajanlar.Contains("*")).ToList();

        return new RobotsKurallari(bizim.SelectMany(g => g.Kurallar)
            .Select(k => (k.Izin, k.Desen, IfadeyeCevir(k.Desen)))
            .ToList());
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

    private sealed record Kayit(RobotsKurallari? Kurallar, DateTimeOffset SonrakiOkuma);

    private readonly ConcurrentDictionary<string, Kayit> kayitlar = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> kapilar = new(StringComparer.OrdinalIgnoreCase);

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
/// İsteği yalnızca adresin robots.txt'si botumuza izin veriyorsa geçirir (bkz. <see cref="RobotsKurallari"/>).
/// İzin verilmeyen istek gönderilmez: nedenini söyleyen yerel bir 403 döner; çekici bunu başarısız kaynak
/// sayar ve mağaza okunmak yerine bayatlar.
/// </summary>
/// <remarks>
/// Shopify istemcilerinde tünel işleyicisinin DIŞINDA durur; robots.txt'nin kendisi de zincirin geri
/// kalanından geçerek okunur, yani Shopify sunucuyu reddettiğinde ev tünelinden.
/// </remarks>
public sealed class RobotsTxtIsleyicisi(RobotsTxtOnbellegi onbellek, ILogger<RobotsTxtIsleyicisi> logger,
    IReadOnlySet<string>? yalnizcaAdresler = null) : DelegatingHandler
{
    /// <summary>robots.txt dosyalarının bize seslendiği ad (WheyProof ile ortak bot).</summary>
    public const string UrunAdi = "wheyproofbot";

    internal const string RetGerekcesi = "Disallowed by robots.txt";

    /// <summary>
    /// TR'nin Shopify mağazaları (puan tazelemesi bunlarda robots'a bakar). Yeni bir Shopify mağazası
    /// eklendiğinde adresi buraya da yazılmalı; çekici istemcileri bu işleyiciyi zaten kendileri takıyor.
    /// </summary>
    public static readonly IReadOnlySet<string> ShopifyAdresleri = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "takehiq.com", "www.commandernutrition.com", "www.supraprotein.com", "supplementfactory.com.tr",
        "gigis.com.tr", "fellasfoods.com.tr", "www.bahsbar.com",
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
            return await base.SendAsync(request, cancellationToken);

        if (kurallar is not null)
            logger.LogWarning("{Host} robots.txt {Yol} yoluna izin vermiyor; istenmedi.", uri.Host, uri.AbsolutePath);
        return new HttpResponseMessage(HttpStatusCode.Forbidden) { ReasonPhrase = RetGerekcesi, RequestMessage = request };
    }
}

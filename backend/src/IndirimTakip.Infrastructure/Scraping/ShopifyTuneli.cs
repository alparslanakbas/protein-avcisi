using System.Net;
using System.Net.Sockets;
using IndirimTakip.Infrastructure.Security;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Shopify isteklerinin ev bağlantısından (WireGuard tüneli) gidebildiği yedek yol.
/// </summary>
/// <remarks>
/// <b>NEDEN (5 Ekim).</b> 29 Eylül'den beri Shopify sunucunun IP'sine (veri merkezi) günün belli
/// saatlerinde bütün platformda 429 veriyor (taramadığımız mağazalarda bile): 3–5 Ekim'de engel
/// öğleden sonra başlayıp sabah 04:00–06:00 UTC arası kalktı, gece turlarında Shopify kaynaklarının
/// hepsi düştü. Aynı anda ev bağlantısı 200 alıyor. Hız düşürmek yetmedi ve tetikleyici puan
/// tazelemesi de değil: 5 Ekim'de engel, o günün tazelemeleri Shopify'a dokunmadan önce başlamıştı.
///
/// <b>Önce doğrudan, 429'da tünel.</b> Tünel yalnızca engel varken kullanılıyor: gündüz ev hattına
/// trafik gitmiyor ve tünel düşerse gündüz turları etkilenmiyor. Bir 429'dan sonra
/// <see cref="YapiskanSure"/> boyunca istekler doğrudan denenmeden tünelden gidiyor; engel saatlerce
/// sürdüğü için her mağazada önce bir 429 daha almak boşuna istek olurdu.
///
/// <b>Vekil.</b> Sunucuda tinyproxy (172.17.0.1:8888, yalnızca Docker köprüsünde, yalnızca 443'e
/// tünel); o sürecin kullanıcısı "ip rule uidrange" ile wg0'a bağlı. Ayar <c>Shopify:Tunel</c>;
/// boşsa tünel kapalı (yerel geliştirme, testler) ve istemciler eskisi gibi çalışıyor.
///
/// <b>SSRF.</b> Tünel istemcisi <see cref="DisAgBaglantisi"/>'nı kullanamaz: soket vekile, yani bir
/// iç adrese bağlanıyor. Onun yerine bağlantı geri çağrısı YALNIZCA ayarlanmış vekile izin veriyor
/// (WebProxy localhost gibi adresleri vekilsiz gönderir; o yol kapalı). Hedefler koddaki sabit
/// mağaza adresleri, kaynaktan gelen adres değil.
///
/// <b>Fiyatlar aynı mı? Ölçüldü (5 Ekim).</b> WheyProof'un 41 Shopify mağazasında tünelden gelen
/// 3.661 fiyatın 3.659'u aynı gün sunucudan doğrudan çekilenle aynı; kalan ikisi aradaki gerçek
/// fiyat değişikliği (Shopify'ın updated_at alanı). Buradaki mağazalar zaten TL satıyor.
/// </remarks>
public sealed class ShopifyTuneli : IDisposable
{
    internal static readonly TimeSpan YapiskanSure = TimeSpan.FromMinutes(30);

    private readonly HttpMessageInvoker? tunel;
    private readonly TimeProvider saat;

    // Bu ana kadar (UTC tick) istekler doğrudan denenmeden tünelden gidiyor.
    private long tunelSonu;

    public ShopifyTuneli(HttpMessageHandler? tunelIsleyicisi, TimeProvider saat)
    {
        tunel = tunelIsleyicisi is null ? null : new HttpMessageInvoker(tunelIsleyicisi, disposeHandler: true);
        this.saat = saat;
    }

    /// <summary>Ayardaki vekil adresinden kurulur; adres boşsa tünel kapalı.</summary>
    public static ShopifyTuneli Olustur(string? vekilAdresi, TimeProvider saat) =>
        new(string.IsNullOrWhiteSpace(vekilAdresi) ? null : IsleyiciOlustur(VekilAdresiniOku(vekilAdresi)), saat);

    public bool Etkin => tunel is not null;

    /// <summary>Son 429'un üzerinden <see cref="YapiskanSure"/> geçmediyse istek doğrudan tünelden gider.</summary>
    public bool TuneldenGitmeli => Etkin && saat.GetUtcNow().UtcTicks < Interlocked.Read(ref tunelSonu);

    /// <summary>Doğrudan istek 429 aldı. Tünel o anda zaten kullanılmıyorsa true (günlüğe bir kez yazılsın).</summary>
    public bool EngelGoruldu()
    {
        var yeni = !TuneldenGitmeli;
        Interlocked.Exchange(ref tunelSonu, (saat.GetUtcNow() + YapiskanSure).UtcTicks);
        return yeni;
    }

    /// <summary>Tünel çalışmadı: sonraki istekler yine önce doğrudan denensin.</summary>
    public void Sifirla() => Interlocked.Exchange(ref tunelSonu, 0);

    public Task<HttpResponseMessage> GonderAsync(HttpRequestMessage istek, CancellationToken cancellationToken) =>
        (tunel ?? throw new InvalidOperationException("Shopify tüneli kapalı.")).SendAsync(istek, cancellationToken);

    public void Dispose() => tunel?.Dispose();

    /// <summary>
    /// Vekil adresi http://host:port biçiminde olmalı. Bozuk ayar sessizce "tünel kapalı"ya
    /// dönmesin diye hata veriyor (üretim ayarını bir test sınıyor).
    /// </summary>
    internal static Uri VekilAdresiniOku(string adres)
    {
        if (!Uri.TryCreate(adres.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0)
            throw new InvalidOperationException($"Shopify:Tunel geçersiz: '{adres}' (beklenen biçim http://host:port).");
        return uri;
    }

    internal static SocketsHttpHandler IsleyiciOlustur(Uri vekil) => new()
    {
        Proxy = new WebProxy(vekil),
        UseProxy = true,
        // Ev hattında sıkıştırma ~10 kat küçültüyor: 250 ürünlük bir katalog sayfası 1,2 MB yerine
        // 115 KB (5 Ekim, ölçüldü).
        AutomaticDecompression = DecompressionMethods.All,
        // Tünel düşükse istek asılı kalmasın; o zaman çekici doğrudan yanıtı (429) görüyor.
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = (baglam, cancellationToken) => VekileBaglanAsync(baglam.DnsEndPoint, vekil, cancellationToken),
    };

    /// <summary>Yalnızca vekile bağlanır; vekilsiz gönderilmek istenen her hedef reddedilir.</summary>
    internal static async ValueTask<Stream> VekileBaglanAsync(DnsEndPoint hedef, Uri vekil, CancellationToken cancellationToken)
    {
        if (!VekilMi(hedef, vekil))
            throw new HttpRequestException($"Shopify tüneli yalnızca vekile bağlanır; '{hedef.Host}:{hedef.Port}' reddedildi.");

        var soket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await soket.ConnectAsync(hedef, cancellationToken);
            return new NetworkStream(soket, ownsSocket: true);
        }
        catch
        {
            soket.Dispose();
            throw;
        }
    }

    internal static bool VekilMi(DnsEndPoint hedef, Uri vekil) =>
        string.Equals(hedef.Host, vekil.IdnHost, StringComparison.OrdinalIgnoreCase) && hedef.Port == vekil.Port;
}

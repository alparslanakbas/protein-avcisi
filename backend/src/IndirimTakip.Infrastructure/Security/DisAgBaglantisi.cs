using System.Net;
using System.Net.Sockets;

namespace IndirimTakip.Infrastructure.Security;

/// <summary>
/// Yalnızca internetteki (genel) adreslere bağlanan HTTP işleyicisi.
/// </summary>
/// <remarks>
/// <b>NEDEN (güvenlik incelemesi, 26 Eylül).</b> Sunucu, kaynaklardan gelen
/// adreslere istek atıyor: ürün görselleri, puan için ürün sayfaları, detay
/// tamamlama, sitemap'teki ürün adresleri. Bu adresleri biz yazmıyoruz; bir
/// mağazanın katalogu ya da sızmış bir ingest anahtarı onları
/// <c>http://169.254.169.254/</c> (bulut sağlayıcının makine bilgisi ucu) ya da
/// <c>http://db:5432</c> gibi İÇ adreslere çevirebilir. O zaman sunucu kendi iç
/// ağını tarayan bir araca dönüşür (SSRF).
///
/// <b>KONTROL BAĞLANTI ANINDA.</b> Adresi istekten önce kontrol etmek yetmezdi:
/// ad önce genel bir IP'ye, bağlantı anında iç IP'ye çözülebilir (DNS
/// rebinding) ve bir yönlendirme (302) iç adrese gidebilir. Burada karar,
/// soketin gerçekten bağlanacağı IP'ye bakılarak veriliyor; yönlendirmeyle
/// açılan her yeni bağlantı da aynı kapıdan geçiyor.
///
/// <b>VARSAYILAN OLARAK HER İSTEMCİDE.</b> <c>ConfigureHttpClientDefaults</c>
/// ile Infrastructure'daki bütün istemcilere uygulanıyor; yeni bir scraper bu
/// korumayı unutamasın diye. İç adrese bilerek giden tek istemci (çıktı
/// önbelleğini ısıtan, <c>localhost</c>) kendi işleyicisini açıkça seçiyor.
/// </remarks>
public static class DisAgBaglantisi
{
    /// <summary>Genel adreslere bağlanan yeni bir işleyici.</summary>
    public static SocketsHttpHandler IsleyiciOlustur() => new()
    {
        // Vekil kullanılsaydı soket vekile bağlanır ve kontrol hedef yerine
        // vekilin adresine bakardı. Sunucuda vekil yok; bilerek kapalı.
        UseProxy = false,
        ConnectCallback = BaglanAsync,
    };

    private static async ValueTask<Stream> BaglanAsync(
        SocketsHttpConnectionContext baglam, CancellationToken cancellationToken)
    {
        var host = baglam.DnsEndPoint.Host;
        var adresler = IPAddress.TryParse(host, out var ip)
            ? [ip]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        // Karışık bir yanıtta (biri genel biri iç) yalnızca genel olanlar
        // deneniyor; iç adres hiçbir koşulda bağlantı adayı olmuyor.
        var izinli = adresler.Where(GenelAdresMi).ToArray();
        if (izinli.Length == 0)
            throw new HttpRequestException($"'{host}' genel bir adrese çözülmüyor; bağlantı kurulmadı.");

        var soket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await soket.ConnectAsync(izinli, baglam.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(soket, ownsSocket: true);
        }
        catch
        {
            soket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// İnternetten erişilebilen bir adres mi? Yerel, özel ağ, bağlantı-yerel
    /// (bulut makine bilgisi ucu dahil), CGNAT, çoklu yayın ve ayrılmış
    /// aralıklar reddediliyor.
    /// </summary>
    internal static bool GenelAdresMi(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return !(b[0] == 0                                  // "bu ağ"
                || b[0] == 10                                   // özel
                || b[0] == 127                                  // yerel
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // CGNAT
                || (b[0] == 169 && b[1] == 254)                 // bağlantı-yerel, makine bilgisi ucu
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // özel (Docker ağları dahil)
                || (b[0] == 192 && b[1] == 168)                 // özel
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)      // IETF ayrılmış
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))  // ölçüm ağları
                || b[0] >= 224);                                // çoklu yayın, ayrılmış, yayın
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(ip.Equals(IPAddress.IPv6Any)
                || ip.IsIPv6LinkLocal
                || ip.IsIPv6SiteLocal
                || ip.IsIPv6UniqueLocal
                || ip.IsIPv6Multicast);
        }

        return false;
    }
}

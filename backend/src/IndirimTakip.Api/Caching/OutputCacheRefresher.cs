using IndirimTakip.Core.Caching;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.OutputCaching;

namespace IndirimTakip.Api.Caching;

/// <summary>
/// Tarama bittikten sonra genel veri önbelleğini etikete göre temizler, sonra
/// en sık istenen uçları bir kez çağırıp yeniden doldurur.
///
/// <b>NEDEN GEREKTİ (3 Eylül'de ölçüldü).</b> Çıktı önbelleği 60 saniyeydi ve
/// trafik düşük olduğu için pratikte HER ziyaretçi soğuk önbelleğe düşüyordu:
/// <c>/api/deals</c> önbellekten 0,26 sn, ıskada <b>2,1 sn</b>; ana sayfa
/// (SSR bu uçları çağırıyor) soğukta <b>6,0 sn</b>, sıcakta 0,26 sn.
///
/// Sürenin %97,7'si PostgreSQL içinde geçiyor (COUNT 654 ms + veri sorgusu
/// 1.437 ms), C# tarafı 49 ms. Yani ASIL çözüm sorgunun kendisi: ürün başına
/// hesaplanan indirim alanlarının tarama sırasında saklanması. O ayrı ve daha
/// riskli bir iş (bkz. CLAUDE.md — <c>DealsQueryService</c> üretimi iki kez
/// düşürdü). Buradaki, o iş yapılana kadar kullanıcının soğuk önbelleğe
/// düşmemesini sağlıyor.
///
/// <b>Yalnızca süreyi uzatmak yetmezdi:</b> uzun süre = bayat veri riski.
/// Etiketli temizleme sayesinde önbellek uzun yaşıyor ama tarama biter bitmez
/// düşüyor — veri hiçbir zaman bir taramadan daha bayat olmuyor.
/// </summary>
public sealed class OutputCacheRefresher(
    IOutputCacheStore cacheStore,
    IHttpClientFactory httpClientFactory,
    IServer server,
    IConfiguration configuration,
    ILogger<OutputCacheRefresher> logger) : IPublicCacheRefresher
{
    /// <summary>Program.cs'teki genel veri politikasına verilen etiket.</summary>
    public const string Tag = "public-data";

    /// <summary>
    /// Isıtılacak adresler — ana sayfanın SSR'ında çağrılan uçlar.
    ///
    /// Politika <c>SetVaryByQuery("*")</c> kullandığı için her farklı sorgu
    /// dizesi AYRI bir önbellek girdisi. Buradaki adresler sayfanın gerçekten
    /// istediği hâlleriyle BİREBİR aynı olmalı; yoksa ısıtma başka bir girdiyi
    /// doldurur ve ziyaretçi yine soğuk önbelleğe düşer.
    /// </summary>
    private static readonly string[] IsitilacakYollar =
    [
        "/api/deals?page=1&pageSize=24",
        "/api/stats",
        "/api/filters",
        "/api/brand-category-pairs",
        "/api/brand-product-counts",
        "/api/preferred-products?take=12",
    ];

    /// <summary>
    /// Uygulamanın KENDİ dinlediği adres. Yapılandırmadaki porta güvenmek
    /// kırılgan: yerelde launch profile 5156 kullanıyor, container'da PORT=8080,
    /// ileride değişebilir. Sunucunun kendi adres listesi her ortamda doğru
    /// cevabı veriyor; yapılandırma yalnızca gerekirse elle geçersiz kılmak
    /// için duruyor.
    /// </summary>
    private string? KendiAdresi()
    {
        var elle = configuration.GetValue<string>("OutputCache:WarmupBaseUrl");
        if (!string.IsNullOrWhiteSpace(elle))
            return elle.TrimEnd('/');

        var adresler = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var adres = adresler?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    ?? adresler?.FirstOrDefault();
        if (adres is null)
            return null;

        // "http://+:8080" / "http://[::]:8080" gibi joker adresler istemci
        // tarafında kullanılamaz; localhost'a çevriliyor.
        return adres.Replace("://+", "://localhost")
                    .Replace("://[::]", "://localhost")
                    .Replace("://0.0.0.0", "://localhost")
                    .TrimEnd('/');
    }

    /// <summary>
    /// <b>ÖNBELLEK ANAHTARI HOST VE ŞEMAYI DA İÇERİYOR.</b> İlk uygulamada
    /// ısıtma <c>http://localhost:8080</c> adresine gidiyordu; istekler 200
    /// dönüyor, log "6/6 uç ısıtıldı" diyordu ama GERÇEK ziyaretçi hâlâ
    /// soğuk önbelleğe düşüyordu. Canlıda ölçülerek bulundu — aynı adres,
    /// üç farklı Host başlığıyla:
    ///
    ///   Host: backend:8080               -> 0,001 sn  (Age 81)
    ///   Host: localhost:8080             -> 0,003 sn  (Age 84)
    ///   Host: api.proteinavcisi.com.tr   -> 2,123 sn  (Age 0)
    ///
    /// Yani ısıtma kimsenin kullanmadığı bir anahtarı dolduruyordu. Şema da
    /// anahtara giriyor: gerçek istekler Caddy'den <c>X-Forwarded-Proto:
    /// https</c> ile geldiği için <c>Request.Scheme</c> "https" oluyor.
    ///
    /// İstek yine makinenin İÇİNDEN gidiyor (Cloudflare'e çıkmıyor); yalnızca
    /// Host ve şema gerçek trafikle aynı olacak şekilde ayarlanıyor.
    /// Yapılandırılmazsa ısıtma yerel anahtarı doldurur — geliştirmede
    /// doğrudur, canlıda <c>OutputCache__WarmupHost</c> verilmelidir.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await cacheStore.EvictByTagAsync(Tag, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Önbellek temizlenemedi; ısıtma yine de denenecek.");
        }

        // Kendi kendine HTTP isteği atıyor: çıktı önbelleği HTTP YANITINI
        // sakladığı için sorgu servisini doğrudan çağırmak önbelleği doldurmaz.
        var taban = KendiAdresi();
        if (taban is null)
        {
            logger.LogWarning("Sunucu adresi bulunamadı; önbellek ısıtma atlandı.");
            return;
        }

        var client = httpClientFactory.CreateClient(nameof(OutputCacheRefresher));
        var basarili = 0;

        // Isıtılan girdinin GERÇEK ziyaretçininkiyle aynı anahtara düşmesi
        // için gereken başlıklar — gerekçesi HerkeseAcikBaslikla'da.
        var acikHost = configuration.GetValue<string>("OutputCache:WarmupHost");
        var acikSema = configuration.GetValue("OutputCache:WarmupScheme", "https");

        foreach (var yol in IsitilacakYollar)
        {
            try
            {
                using var istek = new HttpRequestMessage(HttpMethod.Get, taban + yol);
                if (!string.IsNullOrWhiteSpace(acikHost))
                {
                    istek.Headers.Host = acikHost;
                    istek.Headers.TryAddWithoutValidation("X-Forwarded-Proto", acikSema);
                }

                using var yanit = await client.SendAsync(istek, cancellationToken);
                if (yanit.IsSuccessStatusCode)
                    basarili++;
                else
                    logger.LogWarning("Önbellek ısıtma {Yol} için {Kod} döndü.", yol, (int)yanit.StatusCode);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Isıtma başarısız olsa bile tarama akışı etkilenmemeli —
                // en kötü ihtimalle ilk ziyaretçi eski (yavaş) davranışı görür.
                logger.LogWarning(ex, "Önbellek ısıtma {Yol} için başarısız oldu.", yol);
            }
        }

        logger.LogInformation(
            "Genel veri önbelleği tazelendi: {Basarili}/{Toplam} uç ısıtıldı.",
            basarili, IsitilacakYollar.Length);
    }
}

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

        foreach (var yol in IsitilacakYollar)
        {
            try
            {
                using var yanit = await client.GetAsync(taban + yol, cancellationToken);
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

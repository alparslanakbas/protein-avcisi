namespace IndirimTakip.Core.Caching;

/// <summary>
/// Tarama bittiğinde genel veri önbelleğini tazeler.
///
/// <b>NEDEN BURADA BİR ARAYÜZ VAR.</b> Gerçek iş ASP.NET'in çıktı
/// önbelleğiyle yapılıyor (<c>IOutputCacheStore</c>), ama tetikleyen taraf
/// altyapıdaki tarama servisleri. Somut tipe bağlansaydı Infrastructure
/// projesine tüm web framework'ünü (<c>Microsoft.AspNetCore.App</c>)
/// referans vermek gerekirdi — scraper'ların HTTP sunucusundan haberi olması
/// için hiçbir sebep yok. Arayüz burada, uygulaması Api projesinde.
///
/// Uygulama kayıtlı değilse <see cref="NullPublicCacheRefresher"/> devreye
/// giriyor; tarama hiçbir koşulda önbellek yüzünden düşmüyor.
/// </summary>
public interface IPublicCacheRefresher
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>Önbellek katmanı yokken (testler, konsol araçları) kullanılan boş uygulama.</summary>
public sealed class NullPublicCacheRefresher : IPublicCacheRefresher
{
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

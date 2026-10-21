using System.Text;
using IndirimTakip.Core.Caching;
using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Api.Endpoints;

// Aylık fiyat endeksi (/fiyat-endeksi). Okuma uçları genel veri önbelleğinde; tablo her tarama turunun sonunda
// tazeleniyor (ScrapingBackgroundService), önbellek de aynı turda temizleniyor.
internal static class FiyatEndeksiEndpoints
{
    public static void MapFiyatEndeksiEndpoints(this WebApplication app, string cachePolicy, string? adminApiKey)
    {
        app.MapGet("/api/fiyat-endeksi", async (FiyatEndeksiServisi servis, CancellationToken ct) =>
            Results.Ok(await servis.OkuAsync(ct)))
            .CacheOutput(cachePolicy);

        // İndirilebilir veri (CC BY 4.0). Gazeteci ve araştırmacı için; sayfanın Dataset işaretlemesi buraya bağlanıyor.
        app.MapGet("/api/fiyat-endeksi.csv", async (FiyatEndeksiServisi servis, CancellationToken ct) =>
            Results.File(
                Encoding.UTF8.GetBytes(FiyatEndeksiServisi.Csv(await servis.OkuAsync(ct))),
                "text/csv; charset=utf-8",
                "protein-avcisi-fiyat-endeksi.csv"))
            .CacheOutput(cachePolicy);

        // Elle tazeleme: endeks normalde tarama turunun sonunda hesaplanıyor; deploy'dan hemen sonra ya da yöntem
        // değişince beklemeden hesaplatmak için.
        app.MapPost("/api/dev/fiyat-endeksi/tazele", async (
            FiyatEndeksiServisi servis, IPublicCacheRefresher onbellek, CancellationToken ct) =>
        {
            var satir = await servis.TazeleAsync(DateTimeOffset.UtcNow, ct);
            await onbellek.RefreshAsync(ct);
            return Results.Ok(new { satir });
        }).RequireAdminKey(adminApiKey);
    }
}

using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Api.Endpoints;

// Sezon sayfası (/efsane-kasim): mağazanın indirim dediği ürünlerin yönetmelik ölçütüyle dökümü. Genel veri
// önbelleğinde; tarama sonrası temizlenen etiketle tazeleniyor (bkz. KampanyaIndirimServisi).
internal static class KampanyaEndpoints
{
    public static void MapKampanyaEndpoints(this WebApplication app, string cachePolicy)
    {
        app.MapGet("/api/kampanya/ozet", async (KampanyaIndirimServisi kampanya, CancellationToken ct) =>
            Results.Ok(await kampanya.OzetAsync(ct)))
            .CacheOutput(cachePolicy);

        // Ana sayfa şeridi (6 Kasım): yalnız sayılar. Şerit tam özeti istiyordu; liste 1 MiB'ı geçince SSR'ın yanıt
        // sınırına takılıp ana sayfayı 15 sn bekletti.
        app.MapGet("/api/kampanya/sayilar", async (KampanyaIndirimServisi kampanya, CancellationToken ct) =>
            Results.Ok(await kampanya.SayilarAsync(ct)))
            .CacheOutput(cachePolicy);
    }
}

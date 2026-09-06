using IndirimTakip.Core.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Security;

/// <summary>
/// Güvenlik olaylarını veritabanına yazar.
/// </summary>
public class SecurityEventRecorder(
    AppDbContext db,
    IMemoryCache cache,
    ILogger<SecurityEventRecorder> logger)
{
    /// <summary>Bir adresin bir pencerede yazabileceği en fazla olay sayısı.</summary>
    /// <remarks>
    /// KAYDIN KENDİSİ SALDIRI YÜZEYİ OLMAMALI. Sınır olmasaydı, saniyede
    /// yüzlerce 404 üreten bir tarayıcı saniyede yüzlerce INSERT ürettirirdi —
    /// yani log, saldırganın elinde veritabanını şişirme aracına dönüşürdü.
    /// İlk 30 olay deseni kanıtlamaya zaten yetiyor; sonrası aynı bilgiyi
    /// tekrar ediyor.
    /// </remarks>
    private const int MaxPerWindow = 30;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public async Task RecordAsync(SecurityEvent olay, CancellationToken cancellationToken = default)
    {
        if (!KotaVar(olay.Ip))
            return;

        try
        {
            db.SecurityEvents.Add(olay);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log yazamamak İSTEĞİ BOZMAMALI. Buraya düşen istek zaten hatalı
            // ya da yetkisiz bir istek; üstüne bir de 500 üretmek, saldırgana
            // "burada bir şey kırılıyor" sinyali vermek olurdu.
            logger.LogWarning(ex, "Güvenlik olayı kaydedilemedi: {Kind} {Path}", olay.Kind, olay.Path);
        }
    }

    /// <summary>Adres bu pencerede kotasını doldurmadıysa true.</summary>
    private bool KotaVar(string ip)
    {
        // Bellek içi sayaç, TTL ile kendi kendini temizliyor. Kalıcı bir sözlük
        // tutulsaydı çok sayıda farklı adresten gelen bir saldırıda sözlüğün
        // kendisi bellek sorununa dönüşürdü.
        var sayac = cache.GetOrCreate("guvenlik-olayi:" + ip, giris =>
        {
            giris.AbsoluteExpirationRelativeToNow = Window;
            return new Sayac();
        })!;

        return Interlocked.Increment(ref sayac.Adet) <= MaxPerWindow;
    }

    private sealed class Sayac
    {
        public int Adet;
    }
}

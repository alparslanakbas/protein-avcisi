using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure;

/// <summary>
/// Periyodik bir işi zamanlamasını VERİTABANINDA tutarak çalıştırır: işin sırası
/// kendi tamamlanma kaydından (<see cref="BackgroundJobRun"/>) geliyor.
/// </summary>
/// <remarks>
/// <b>NEDEN.</b> Tarama ve puan tazeleme
/// <c>do { çalış } while (await timer.WaitForNextTickAsync())</c> olarak yazılmıştı:
/// her açılışta bir tur, sonra süreç belleğinde bir sayaç. Deploy konteyneri
/// yeniden oluşturduğu için HER DEPLOY bütün kaynakların tam taramasını
/// başlatıyor ve takvimi sıfırlıyordu. Aynı kod WheyProof'ta ölçüldü: bir
/// önceki turdan 45 dk sonra deploy'un başlattığı turda üç mağaza 429 verdi,
/// 6 saat sonraki normal turda hiçbiri vermedi (19 Eylül). Bu depoda ayrıca
/// gece taramasına denk gelen deploy yasağının bir parçası da bu davranıştı.
///
/// Artık açılış, işi yalnızca son TAMAMLANAN turdan bu yana aralık dolduysa
/// çalıştırıyor, dolmadıysa kalan süreyi bekliyor. Damga yalnızca tur bitince
/// yazılıyor: deploy'un yarıda kestiği tur eski damgayı bırakıyor ve sonraki
/// açılış onu hemen yeniden çalıştırıyor — iptal edilen tarama hiçbir şey
/// kaydetmiyor, o günün fiyatları bir aralık beklememeli.
///
/// <b>GÜNLÜK İŞ (4 Ekim).</b> Gece yarısı taraması sabit saatte çalıştığı için
/// süreç belleğinde bir sonraki 21:00'e kadar bekliyordu: deploy onu yarıda
/// keserse ertesi gece yarısına kadar hiç çalışmıyordu (2 Eylül'de Provitamin'in
/// gecelik verisi böyle kayboldu). <see cref="RunDailyAsync"/> aynı damgayı
/// kullanıyor: son tamamlanma en son planlı saatten önceyse iş hemen çalışıyor.
/// </remarks>
public static class PersistedSchedule
{
    /// <summary>İşin sırası gelene kadar kalan süre; sırası geldiyse sıfır.</summary>
    internal static TimeSpan TimeUntilDue(DateTimeOffset? lastCompleted, TimeSpan interval, DateTimeOffset now)
    {
        if (lastCompleted is null)
            return TimeSpan.Zero;

        var remaining = lastCompleted.Value + interval - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Her gün <paramref name="runAtUtcHour"/>'da çalışan işin sırasına kalan süre.
    /// En son planlı saatten bu yana tamamlanmadıysa (deploy kesti, sunucu kapalıydı)
    /// sıfır. Hiç kaydı yoksa (iş ilk kez bu takvimle açılıyor) bir sonraki planlı
    /// saat: gece taramasını gün ortasındaki bir deploy'da başlatmamak için.
    /// </summary>
    internal static TimeSpan TimeUntilDailyDue(DateTimeOffset? lastCompleted, int runAtUtcHour, DateTimeOffset now)
    {
        var bugun = new DateTimeOffset(now.UtcDateTime.Date.AddHours(runAtUtcHour), TimeSpan.Zero);
        var sonPlanli = now >= bugun ? bugun : bugun.AddDays(-1);
        if (lastCompleted is not null && lastCompleted < sonPlanli)
            return TimeSpan.Zero;

        return sonPlanli.AddDays(1) - now;
    }

    /// <summary>
    /// Sıra gelene kadar bekler, işi çalıştırır, tamamlanmayı kaydeder ve uygulama
    /// kapanana kadar tekrarlar. <paramref name="run"/> her seferinde yeni bir kapsamla çağrılır.
    /// </summary>
    public static Task RunAsync(
        IServiceScopeFactory scopeFactory,
        string jobName,
        TimeSpan interval,
        ILogger logger,
        Func<IServiceProvider, CancellationToken, Task> run,
        CancellationToken stoppingToken) =>
        RunCoreAsync(scopeFactory, jobName, (last, now) => TimeUntilDue(last, interval, now), logger, run, stoppingToken);

    /// <summary><see cref="RunAsync"/> gibi, ama her gün sabit bir saatte (UTC).</summary>
    public static Task RunDailyAsync(
        IServiceScopeFactory scopeFactory,
        string jobName,
        int runAtUtcHour,
        ILogger logger,
        Func<IServiceProvider, CancellationToken, Task> run,
        CancellationToken stoppingToken) =>
        RunCoreAsync(scopeFactory, jobName, (last, now) => TimeUntilDailyDue(last, runAtUtcHour, now), logger, run, stoppingToken);

    private static async Task RunCoreAsync(
        IServiceScopeFactory scopeFactory,
        string jobName,
        Func<DateTimeOffset?, DateTimeOffset, TimeSpan> timeUntilDue,
        ILogger logger,
        Func<IServiceProvider, CancellationToken, Task> run,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var lastCompleted = await ReadLastCompletedAsync(scopeFactory, jobName, stoppingToken);
                var wait = timeUntilDue(lastCompleted, DateTimeOffset.UtcNow);
                if (wait > TimeSpan.Zero)
                {
                    logger.LogInformation(
                        "{Job}: son tamamlanma {LastCompleted:u}, sıradaki tur {Wait} sonra.",
                        jobName, lastCompleted, wait);
                    await Task.Delay(wait, stoppingToken);
                }

                using (var scope = scopeFactory.CreateScope())
                {
                    await run(scope.ServiceProvider, stoppingToken);
                }

                // Buraya yalnızca iş döndüyse gelinir: iptal edilen tur bu satırı
                // atlayarak fırlar ve önceki damga yerinde kalır.
                await MarkCompletedAsync(scopeFactory, jobName, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // BackgroundService'ten kaçan istisna varsayılan olarak bütün
                // uygulamayı durdurur; yeniden açılış da işi baştan başlatırdı.
                // Damgayı okurken/yazarken bir anlık veritabanı hatası loglanıp bir
                // dakika sonra yeniden deneniyor.
                logger.LogError(ex, "{Job}: zamanlama başarısız, bir dakika sonra yeniden denenecek.", jobName);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private static async Task<DateTimeOffset?> ReadLastCompletedAsync(
        IServiceScopeFactory scopeFactory, string jobName, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BackgroundJobRuns
            .Where(j => j.JobName == jobName)
            .Select(j => (DateTimeOffset?)j.LastCompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task MarkCompletedAsync(
        IServiceScopeFactory scopeFactory, string jobName, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.BackgroundJobRuns.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken);
        if (record is null)
            db.BackgroundJobRuns.Add(new BackgroundJobRun { JobName = jobName, LastCompletedAt = DateTimeOffset.UtcNow });
        else
            record.LastCompletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}

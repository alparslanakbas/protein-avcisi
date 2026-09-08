using IndirimTakip.Core.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Security;

/// <summary>
/// Başarısız yönetim işlemlerini veritabanına yazar.
/// </summary>
/// <remarks>
/// <b>KENDİ KAPSAMINI AÇIYOR — bu şart, kolaylık değil.</b> Kaydedilecek
/// hataların bir kısmı zaten <c>SaveChangesAsync</c> patladığı için oluşuyor.
/// İsteğin kendi <c>AppDbContext</c>'i o anda kirli: başarısız olan varlıklar
/// hâlâ izleniyor. Aynı bağlam üzerinden kayıt atmak, o başarısız yazmayı
/// TEKRAR denemek demek olurdu — yani tam da açıklamaya çalıştığımız hata
/// kaydı, aynı hataya takılıp kaybolurdu. Temiz bir kapsam bu bağı kesiyor.
/// </remarks>
public class AdminFailureRecorder(
    IServiceScopeFactory scopeFactory,
    ILogger<AdminFailureRecorder> logger)
{
    public async Task RecordAsync(AdminOperationFailure kayit, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.AdminOperationFailures.Add(kayit);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Kayıt tutulamaması isteğin sonucunu DEĞİŞTİRMEMELİ. Kullanıcı
            // zaten bir hata alıyor; üstüne bir de log yüzünden ikinci bir
            // hata üretmek asıl sebebi büsbütün gizlerdi.
            logger.LogWarning(ex, "Yönetim hatası kaydedilemedi: {Method} {Path} {Status}",
                kayit.Method, kayit.Path, kayit.StatusCode);
        }
    }
}

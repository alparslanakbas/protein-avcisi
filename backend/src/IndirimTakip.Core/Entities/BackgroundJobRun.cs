namespace IndirimTakip.Core.Entities;

/// <summary>
/// Periyodik bir arka plan işinin EN SON BAŞARIYLA TAMAMLANDIĞI an.
/// </summary>
/// <remarks>
/// <b>NEDEN AYRI BİR KAYIT GEREKTİ.</b> Detay tamamlama işi "sıram geldi mi"
/// sorusunu <c>MAX(Products.NutritionCheckedAt)</c> ile cevaplıyordu. Mantık
/// şuydu: o damgayı yalnızca bu iş yazıyor, dolayısıyla en yeni damga son
/// çalışma zamanıdır. Doğru görünüyor ama BİR DURUMU KAÇIRIYOR — tur yarıda
/// kesilirse.
///
/// 6 Eylül'de canlıda yaşandı: tur 18:58'de başladı, <b>tek ürün</b> işledi ve
/// deploy konteyneri yenileyince iptal oldu. O tek damga MAX'ı ilerlettiği
/// için sıradaki tur tam bir aralık ötelendi. Yoğun deploy yapılan bir günde
/// iş neredeyse hiç ilerlemeden sürekli ertelenebilirdi.
///
/// <b>Neden sinsi:</b> besin serisi alarmı bunu yakalamıyor. O alarm "5+ ürüne
/// bakıldı ama besin 0 arttı" diyor; burada bakılan sayısı da artmıyor, yani
/// alarm açısından hiçbir şey olmamış gibi görünüyor.
///
/// Damga artık turun KENDİ tamamlanmasına bağlı: yarıda kesilen tur bu kaydı
/// güncellemiyor ve sıra ilerlemiyor.
///
/// <b>Neden bellekte değil:</b> periyot günler mertebesinde. Süreç belleğinde
/// tutulsaydı her deploy sayacı sıfırlar ve periyot hiç dolmazdı — bültende
/// tam olarak bu yaşandı (bkz. DigestBackgroundService).
/// </remarks>
public class BackgroundJobRun
{
    public int Id { get; set; }

    /// <summary>İşin sabit adı; benzersiz.</summary>
    public required string JobName { get; set; }

    public DateTimeOffset LastCompletedAt { get; set; }
}

public static class BackgroundJobNames
{
    public const string DetayTamamlama = "detay-tamamlama";
}

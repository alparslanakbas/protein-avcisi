namespace IndirimTakip.Core.Entities;

// Güvenlik olayı kaydı — kötüye kullanımı görmek ve gerektiğinde suç
// duyurusuna dayanak oluşturmak için.
//
// ÖNCEKİ KARAR BİLEREK TERSİNE ÇEVRİLDİ. 2026-08-15'te hassas uçlara istek
// logu eklenirken koda "ayrı bir DB tablosu kurmak burada aşırı mühendislik
// olurdu" diye yazılmıştı ve O GÜN DOĞRUYDU: amaç yalnızca stdout'a bir iz
// bırakmaktı. Amaç 6 Eylül'de değişti — kaydın SORGULANABİLİR, KALICI ve
// savunulabilir olması isteniyor. Docker'ın stdout logu üçünü de
// karşılamıyor: döner, filtrelenemez ve konteyner yenilenince kaybolur.
//
// HER İSTEK KAYDEDİLMİYOR — yalnızca dikkate değer olanlar. Normal sayfa
// görüntülemeleri buraya HİÇ girmiyor; girseydi hem hacim yönetilemez olurdu
// hem de amaca hizmet etmeyen kişisel veri biriktirmiş olurduk. Bir kaydın
// hukuken savunulabilir olması için amacının dar ve tanımlı olması gerekiyor.
public class SecurityEvent
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Gerçek istemci adresi (Cloudflare'in CF-Connecting-IP başlığı).
    /// Origin kilidinden sonra TCP seviyesindeki adres HER ZAMAN Cloudflare'e
    /// ait olduğu için bu başlık tek doğru kaynak.
    /// </summary>
    public required string Ip { get; set; }

    /// <summary>
    /// Olay türü: <c>unauthorized</c> (yetkisiz deneme), <c>rate-limited</c>
    /// (hız sınırı), <c>probe</c> (bilinen açık taraması),
    /// <c>server-error</c> (sunucu hatası).
    /// </summary>
    public required string Kind { get; set; }

    public required string Method { get; set; }

    public required string Path { get; set; }

    public int StatusCode { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>
    /// Cloudflare'in CF-IPCountry başlığı. Suç duyurusunda yurt içi/yurt dışı
    /// ayrımı doğrudan hangi mercie başvurulacağını belirlediği için tutuluyor.
    /// </summary>
    public string? Country { get; set; }
}

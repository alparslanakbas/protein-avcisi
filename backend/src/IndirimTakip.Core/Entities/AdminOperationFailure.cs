namespace IndirimTakip.Core.Entities;

/// <summary>
/// Yönetim ucuna (<c>/api/dev/*</c>) gelen bir isteğin BAŞARISIZLIK SEBEBİ.
/// </summary>
/// <remarks>
/// <b>NEDEN GEREKTİ.</b> 8 Eylül'de panelden kupon eklenemedi ve ekranda tek
/// gördüğümüz "Kupon eklenmedi." oldu. Gerçek sebep — katalogdaki ad
/// "Dr Supplement", yazılan ad "DrSupplement" — yalnızca elle <c>curl</c>
/// atınca ortaya çıktı. Aynı hafta ikinci bir örnek daha yaşandı: Türkiye
/// saat dilimiyle gönderilen kupon tarihi Npgsql'de 500 üretiyordu ve o
/// mesaj da yalnızca konteynerin stdout'unda duruyordu.
///
/// <b>NEDEN SecurityEvents'E YAZILMIYOR.</b> O tablonun amacı dar ve
/// tanımlı: kötüye kullanım kanıtı, gerektiğinde suç duyurusuna dayanak.
/// Buraya yazılan kayıtlar ise YÖNETİCİNİN KENDİ işlemleri. Üç somut zarar
/// doğardı:
/// <list type="bullet">
///   <item>Panelin "en çok olay üreten 10 adres" listesi — bir suç
///   duyurusunda ilk bakılan yer — yöneticinin kendi adresiyle dolardı.</item>
///   <item>Oradaki kota (adres başına 5 dakikada 30 olay) düşmanca trafiğe
///   göre ayarlı; peş peşe denenen birkaç yönetim işleminde tam da ihtiyaç
///   duyulan kayıt sessizce düşebilirdi.</item>
///   <item>SecurityEvents BİLEREK mesaj/sorgu saklamıyor; oraya serbest
///   metin bir alan eklemek, kaydın "dar tutuluyor" gerekçesini zayıflatırdı.
///   Burada mesajı saklamak güvenli, çünkü kaydı üreten kimlik doğrulanmış
///   yöneticinin kendisi.</item>
/// </list>
///
/// <b>YALNIZCA BAŞARISIZLIK.</b> Başarılı işlemler kaydedilmiyor: sorulan
/// soru "neden olmadı", ve başarılı işlemin sonucu zaten verinin kendisinde
/// görünüyor.
/// </remarks>
public class AdminOperationFailure
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public required string Method { get; set; }

    public required string Path { get; set; }

    public int StatusCode { get; set; }

    /// <summary>
    /// Ucun kendi cevabı (ör. "'DrSupplement' adında marka bulunamadı.") ya da
    /// işlem istisnayla düştüyse istisnanın türü ve mesajı. Uç gövdesiz bir
    /// hata döndüyse null.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// İsteği yapan adres. Yönetici tek kişi ama betiklerin ve panelin
    /// ürettiği hatayı ayırt etmeye yarıyor.
    /// </summary>
    public string? Ip { get; set; }
}

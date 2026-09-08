using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace IndirimTakip.Infrastructure.Security;

/// <summary>
/// Başarısız bir yönetim işleminin kayda geçecek SEBEP metnini üretir.
/// </summary>
/// <remarks>
/// Saf fonksiyon olarak ayrıldı, çünkü bu özelliğin tamamı buna bağlı: sebep
/// yanlış çıkarılırsa panelde boş bir sütun kalır ve kullanıcı yine tahmin
/// etmek zorunda kalır — yani kayıt var ama işe yaramaz. Saf olduğu için
/// teste bağlanabiliyor.
/// </remarks>
public static class AdminFailureReason
{
    /// <summary>Sebep metninin ve <c>Reason</c> kolonunun sınırı.</summary>
    public const int EnFazlaUzunluk = 2000;

    // TÜRKÇE TUZAĞI. System.Text.Json VARSAYILAN OLARAK ASCII dışındaki her
    // karakteri kaçırıyor: "'DrSupplement' adında marka bulunamadı" cümlesi
    // JSON'a çevrilince "bulunamadı" oluyor. Kayıt teknik olarak doğru
    // ama panelde okunmuyor — yani hatayı açıklamak için tutulan alan
    // okunamaz hâle geliyor. Kaçış Türkçe blokta gevşetiliyor.
    private static readonly JsonSerializerOptions JsonSecenekleri = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement,
            UnicodeRanges.LatinExtendedA),
    };

    /// <summary>
    /// Ucun döndürdüğü değerden sebep metni. Metin olmayan değerler JSON'a
    /// çevriliyor: alan adını tahmin etmeye çalışmaktan sağlam, çünkü uç
    /// hangi biçimi kullanırsa kullansın metin kayda giriyor.
    /// </summary>
    public static string? Degerden(object? deger)
    {
        if (deger is null)
            return null;

        if (deger is string metin)
            return Kirp(metin);

        try
        {
            return Kirp(JsonSerializer.Serialize(deger, JsonSecenekleri));
        }
        catch (Exception)
        {
            // Serileştirilemeyen bir değer yüzünden kaydın tamamını kaybetmek
            // istemiyoruz; durum kodu ve yol yine kaydediliyor.
            return null;
        }
    }

    /// <summary>İstisnadan sebep metni.</summary>
    public static string Istisnadan(Exception ex)
    {
        var metin = ex.GetType().Name + ": " + ex.Message;

        // ASIL SEBEP ÇOĞU ZAMAN İÇERİDE. EF ve Npgsql hatayı sarmalıyor;
        // 7 Eylül'deki "only offset 0 (UTC) is supported" tam olarak böyle bir
        // iç istisnaydı ve dış mesaj tek başına hiçbir şey söylemiyordu.
        if (ex.InnerException is { } ic)
            metin += " → " + ic.GetType().Name + ": " + ic.Message;

        return Kirp(metin)!;
    }

    private static string? Kirp(string? deger)
    {
        if (string.IsNullOrEmpty(deger))
            return null;

        return deger.Length <= EnFazlaUzunluk ? deger : deger[..EnFazlaUzunluk];
    }
}

using IndirimTakip.Infrastructure.Security;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Sebep metni üretimi. Bu özelliğin tamamı buna bağlı: sebep boş ya da
/// okunmaz çıkarsa panelde kayıt görünür ama işe yaramaz — kullanıcı yine
/// tahmin etmek zorunda kalır.
/// </summary>
public class AdminFailureReasonTests
{
    [Fact]
    public void Metin_oldugu_gibi_geciyor()
    {
        var sebep = AdminFailureReason.Degerden("'DrSupplement' adında marka bulunamadı.");

        Assert.Equal("'DrSupplement' adında marka bulunamadı.", sebep);
    }

    [Fact]
    public void Bos_deger_null_donuyor()
    {
        Assert.Null(AdminFailureReason.Degerden(null));
        Assert.Null(AdminFailureReason.Degerden(string.Empty));
    }

    /// <summary>
    /// TÜRKÇE TUZAĞI. System.Text.Json varsayılan olarak ASCII dışını
    /// kaçırıyor; bu test olmasaydı panelde "bulunamadı" görünürdü —
    /// kayıt teknik olarak doğru, insan için okunmaz.
    /// </summary>
    [Fact]
    public void Nesne_JSONa_cevrilirken_turkce_harfler_kacirilmiyor()
    {
        var sebep = AdminFailureReason.Degerden(new { message = "Marka bulunamadı: İçecek Ürünleri" });

        Assert.NotNull(sebep);
        Assert.Contains("bulunamadı", sebep);
        Assert.Contains("İçecek Ürünleri", sebep);
        Assert.DoesNotContain("\\u", sebep);
    }

    [Fact]
    public void Uzun_metin_kolon_sinirinda_kirpiliyor()
    {
        var sebep = AdminFailureReason.Degerden(new string('x', AdminFailureReason.EnFazlaUzunluk + 500));

        Assert.Equal(AdminFailureReason.EnFazlaUzunluk, sebep!.Length);
    }

    /// <summary>
    /// 7 Eylül'de yaşanan gerçek durum: Npgsql'in "only offset 0 (UTC) is
    /// supported" mesajı İÇ istisnadaydı, dış istisna hiçbir şey söylemiyordu.
    /// </summary>
    [Fact]
    public void Ic_istisnanin_mesaji_da_kayda_giriyor()
    {
        var ic = new ArgumentException("Cannot write DateTimeOffset with Offset=+03:00 to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported.");
        var dis = new InvalidOperationException("An error occurred while saving the entity changes.", ic);

        var sebep = AdminFailureReason.Istisnadan(dis);

        Assert.Contains("InvalidOperationException", sebep);
        Assert.Contains("ArgumentException", sebep);
        Assert.Contains("only offset 0 (UTC) is supported", sebep);
    }

    [Fact]
    public void Ic_istisna_yoksa_tek_satir_kaliyor()
    {
        var sebep = AdminFailureReason.Istisnadan(new InvalidOperationException("tek başına"));

        Assert.Equal("InvalidOperationException: tek başına", sebep);
    }

    [Fact]
    public void Cok_uzun_istisna_metni_de_kirpiliyor()
    {
        var sebep = AdminFailureReason.Istisnadan(new InvalidOperationException(new string('y', 5000)));

        Assert.Equal(AdminFailureReason.EnFazlaUzunluk, sebep.Length);
    }
}

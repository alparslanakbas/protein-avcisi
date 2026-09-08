using IndirimTakip.Infrastructure.Images;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Dosya adı üretimi ve adres çözümü. İkisi de saf; asıl indirme/küçültme
/// ağ ve disk gerektirdiği için canlıya karşı ölçüldü.
/// </summary>
public class ProductImageStoreTests
{
    /// <summary>
    /// Ad kaynak adresin özeti olduğu için AYNI adres her zaman aynı adı
    /// vermeli — "indirilmiş mi" sorusunun cevabı buna dayanıyor.
    /// </summary>
    [Fact]
    public void Ayni_adres_ayni_dosya_adini_veriyor()
    {
        var a = ProductImageStore.DosyaAdi("https://proteinim.com/wp-content/uploads/2026/06/L-Glutamine-Powder.jpg");
        var b = ProductImageStore.DosyaAdi("https://proteinim.com/wp-content/uploads/2026/06/L-Glutamine-Powder.jpg");

        Assert.Equal(a, b);
        Assert.EndsWith(".webp", a);
    }

    [Fact]
    public void Farkli_adres_farkli_dosya_adi()
    {
        var a = ProductImageStore.DosyaAdi("https://ornek.com/a.jpg");
        var b = ProductImageStore.DosyaAdi("https://ornek.com/b.jpg");

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Ad kolonun sınırına (64) rahatça sığmalı; sığmazsa kayıt kesilir ve
    /// dosya bir daha bulunamaz.
    /// </summary>
    [Fact]
    public void Dosya_adi_kolon_sinirina_siğiyor()
    {
        var ad = ProductImageStore.DosyaAdi("https://ornek.com/" + new string('u', 2000) + ".jpg");

        Assert.True(ad.Length <= 64, $"dosya adı {ad.Length} karakter");
    }

    [Theory]
    [InlineData("https://api.proteinavcisi.com.tr/api/gorsel")]
    // Sondaki eğik çizgi çift eğik çizgi üretmemeli.
    [InlineData("https://api.proteinavcisi.com.tr/api/gorsel/")]
    public void Genel_adres_dogru_birlestiriliyor(string taban)
    {
        var adres = ProductImageStore.GenelAdres("abc123.webp", taban);

        Assert.Equal("https://api.proteinavcisi.com.tr/api/gorsel/abc123.webp", adres);
    }

    /// <summary>
    /// Yerel kopya yokken null dönmeli: çağıran taraf bu durumda KAYNAK
    /// adrese düşüyor, yani indirme tamamlanana kadar site eskisi gibi
    /// çalışıyor.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Yerel_kopya_yoksa_null(string? yerel)
    {
        Assert.Null(ProductImageStore.GenelAdres(yerel, "https://ornek.com/gorsel"));
    }
    /// <summary>
    /// ASIL İŞ BU: 1200 px bir görsel 400 px'e inmeli ve WebP olarak
    /// yazılmalı. Kütüphane yolu (kod çözme, yeniden boyutlandırma, WebP
    /// kodlama) deploy'dan önce en az bir kez GERÇEKTEN çalışmalı.
    /// </summary>
    [Fact]
    public async Task Buyuk_gorsel_400_pikselde_webp_oluyor()
    {
        using var girdi = SahteGorsel(1200, 900);

        var webp = await ProductImageStore.KucultAsync(girdi, 78, CancellationToken.None);

        Assert.NotEmpty(webp);
        // WebP kabı: "RIFF" + boyut + "WEBP".
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(webp, 0, 4));
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(webp, 8, 4));

        using var sonuc = SixLabors.ImageSharp.Image.Load(webp);
        Assert.Equal(ProductImageStore.EnFazlaKenar, sonuc.Width);
        Assert.Equal(300, sonuc.Height); // 1200x900 -> 400x300, en-boy korunuyor
    }

    /// <summary>
    /// Sınırın altındaki görsel BÜYÜTÜLMEMELİ — büyütmek bayt ekler,
    /// görüntü eklemez.
    /// </summary>
    [Fact]
    public async Task Kucuk_gorsel_buyutulmuyor()
    {
        using var girdi = SahteGorsel(150, 150);

        var webp = await ProductImageStore.KucultAsync(girdi, 78, CancellationToken.None);

        using var sonuc = SixLabors.ImageSharp.Image.Load(webp);
        Assert.Equal(150, sonuc.Width);
        Assert.Equal(150, sonuc.Height);
    }

    private static MemoryStream SahteGorsel(int genislik, int yukseklik)
    {
        using var gorsel = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(genislik, yukseklik);
        gorsel.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.CornflowerBlue));

        var akis = new MemoryStream();
        gorsel.Save(akis, new PngEncoder());
        akis.Position = 0;
        return akis;
    }

}

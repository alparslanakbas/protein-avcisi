using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace IndirimTakip.Infrastructure.Images;

/// <summary>
/// Ürün görsellerini kendi sunucumuza indirip küçültür.
/// </summary>
/// <remarks>
/// <b>NEDEN GEREKTİ (8 Eylül).</b> Görseller 36 ayrı kaynağın CDN'inden
/// doğrudan bağlanıyordu (hotlink). Ölçüldü: kaynak başına birer örnekte
/// ortalama <b>280 kB</b>, en büyüğü <b>2,35 MB</b> — 24 ürünlük bir liste
/// sayfası tek başına ~6,7 MB görsel indiriyordu. Ayrıca hotlink sessizce
/// bozulabiliyor: erişilemeyen bir host <c>onerror</c>'ı TETİKLEMİYOR, istek
/// asılı kalıyor ve yedek görsel hiç devreye girmiyor (aynı sorun marka
/// logolarında yaşanmış ve onlar da indirilmişti).
///
/// <b>NEDEN 400 PİKSEL.</b> Liste kartındaki kutu 78-92 px, öne çıkan ürün
/// 120 px. 2x ekranda en fazla ~240 px gerekiyor; 400 px hem ürün sayfasına
/// hem ileride büyütülebilecek bir karta pay bırakıyor. Kaynak görsel daha
/// küçükse BÜYÜTÜLMÜYOR — büyütmek bayt ekler, görüntü eklemez.
///
/// <b>DOSYA ADI KAYNAK ADRESİN ÖZETİ.</b> Ürün kimliği kullanılmadı: kaynak
/// aynı görseli birden çok üründe kullanabiliyor (bayilerde sık) ve adres
/// değişince eski dosyanın geçersizleştiği kendiliğinden anlaşılıyor.
/// Uzantı her zaman .webp, çünkü çıktı biçimi tek.
/// </remarks>
public sealed class ProductImageStore(
    IHttpClientFactory httpClientFactory,
    ProductImageOptions options,
    ILogger<ProductImageStore> logger)
{
    public const string HttpClientAdi = "urun-gorseli";

    /// <summary>Uzun kenarın en fazla piksel değeri.</summary>
    public const int EnFazlaKenar = 400;

    /// <summary>
    /// Kaynak adres için üretilecek yerel dosya adı. Saf fonksiyon: aynı
    /// adres her zaman aynı adı veriyor.
    /// </summary>
    public static string DosyaAdi(string kaynakAdres)
    {
        var ozet = SHA256.HashData(Encoding.UTF8.GetBytes(kaynakAdres.Trim()));
        return Convert.ToHexStringLower(ozet.AsSpan(0, 12)) + ".webp";
    }

    /// <summary>Yerel dosyanın herkese açık adresi; dosya adı yoksa null.</summary>
    public static string? GenelAdres(string? dosyaAdi, string tabanAdres) =>
        string.IsNullOrEmpty(dosyaAdi) ? null : $"{tabanAdres.TrimEnd('/')}/{dosyaAdi}";

    /// <summary>
    /// Görseli indirip küçülterek diske yazar; başarılıysa dosya adını,
    /// değilse null döndürür.
    /// </summary>
    public async Task<string?> IndirAsync(string kaynakAdres, CancellationToken cancellationToken)
    {
        var dosyaAdi = DosyaAdi(kaynakAdres);
        var hedef = Path.Combine(options.Dizin, dosyaAdi);

        // Zaten varsa yeniden indirilmiyor: tur yarıda kesilip tekrar
        // başladığında aynı işi baştan yapmamalı.
        if (File.Exists(hedef))
            return dosyaAdi;

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientAdi);
            using var yanit = await client.GetAsync(
                kaynakAdres, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!yanit.IsSuccessStatusCode)
                return null;

            // Sunucu boyutu bildiriyorsa ve tavanı aşıyorsa gövde hiç
            // indirilmiyor. Bildirmiyorsa aşağıdaki okuma sınırı koruyor.
            if (yanit.Content.Headers.ContentLength > options.EnFazlaBayt)
                return null;

            await using var kaynak = await yanit.Content.ReadAsStreamAsync(cancellationToken);
            using var tampon = new MemoryStream();
            await KopyalaAsync(kaynak, tampon, options.EnFazlaBayt, cancellationToken);
            tampon.Position = 0;

            var webp = await KucultAsync(tampon, options.Kalite, cancellationToken);

            Directory.CreateDirectory(options.Dizin);

            // ÖNCE GEÇİCİ DOSYA, SONRA TAŞIMA. Doğrudan hedefe yazılsaydı,
            // yazma sürerken gelen bir istek yarım dosya görürdü ve o bozuk
            // görsel tarayıcının önbelleğine girerdi.
            var gecici = hedef + ".tmp";
            await File.WriteAllBytesAsync(gecici, webp, cancellationToken);
            File.Move(gecici, hedef, overwrite: true);
            return dosyaAdi;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Tek bir görselin başarısızlığı turu düşürmemeli: kaynak adresi
            // ölmüş ya da biçim bozuk olabilir. O ürün yerel kopyasız kalıyor
            // ve KAYNAK adresiyle gösterilmeye devam ediyor.
            logger.LogDebug(ex, "Ürün görseli indirilemedi: {Adres}", kaynakAdres);
            return null;
        }
    }

    /// <summary>
    /// Görseli uzun kenarı en fazla <see cref="EnFazlaKenar"/> olacak şekilde
    /// küçültüp WebP'e çevirir. Ağ ve diskten ayrı tutuldu ki asıl dönüşüm
    /// teste bağlanabilsin.
    /// </summary>
    internal static async Task<byte[]> KucultAsync(Stream girdi, int kalite, CancellationToken cancellationToken)
    {
        using var gorsel = await Image.LoadAsync(girdi, cancellationToken);

        // KÜÇÜKSE BÜYÜTÜLMÜYOR: Max modu yalnızca sınırı aşan kenarı
        // indiriyor, zaten küçük olan görsel olduğu gibi kalıyor. Büyütmek
        // bayt ekler, görüntü eklemez.
        if (gorsel.Width > EnFazlaKenar || gorsel.Height > EnFazlaKenar)
        {
            gorsel.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(EnFazlaKenar, EnFazlaKenar),
            }));
        }

        using var cikti = new MemoryStream();
        await gorsel.SaveAsync(cikti, new WebpEncoder { Quality = kalite }, cancellationToken);
        return cikti.ToArray();
    }

    /// <summary>Artık hiçbir ürünün kullanmadığı dosyaları siler.</summary>
    public int KullanilmayanlariSil(IReadOnlySet<string> kullanilanlar)
    {
        if (!Directory.Exists(options.Dizin))
            return 0;

        var silinen = 0;
        foreach (var yol in Directory.EnumerateFiles(options.Dizin, "*.webp"))
        {
            if (kullanilanlar.Contains(Path.GetFileName(yol)))
                continue;

            try
            {
                File.Delete(yol);
                silinen++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Artık görsel silinemedi: {Yol}", yol);
            }
        }

        return silinen;
    }

    private static async Task KopyalaAsync(Stream kaynak, Stream hedef, long enFazla, CancellationToken ct)
    {
        var tampon = new byte[81920];
        long toplam = 0;
        int okunan;
        while ((okunan = await kaynak.ReadAsync(tampon, ct)) > 0)
        {
            toplam += okunan;
            if (toplam > enFazla)
                throw new InvalidOperationException("Görsel boyut sınırını aştı.");

            await hedef.WriteAsync(tampon.AsMemory(0, okunan), ct);
        }
    }
}

public sealed class ProductImageOptions
{
    /// <summary>Görsellerin yazılacağı dizin (konteyner içi yol).</summary>
    public string Dizin { get; set; } = "/app/urun-gorsel";

    /// <summary>Yerel görsellerin herkese açık adres öneki.</summary>
    public string TabanAdres { get; set; } = "https://api.proteinavcisi.com.tr/api/gorsel";

    public bool Enabled { get; set; } = true;

    /// <summary>Bir turda indirilecek en fazla görsel.</summary>
    public int TurBasinaAdet { get; set; } = 150;

    public int AralikDakika { get; set; } = 10;

    public int Kalite { get; set; } = 78;

    /// <summary>İndirilecek en büyük kaynak dosya (bayt).</summary>
    public long EnFazlaBayt { get; set; } = 12 * 1024 * 1024;
}

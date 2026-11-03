namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// Markaların ortaklık programı ayarları. Kodlar yapılandırmadan (VM'deki
/// .env) geliyor, repoya girmiyor: hesaba bağlı bilgiler.
/// Örnek: Affiliate__TrackingCodes__Hardline=765c5a13e1
/// </summary>
public sealed class AffiliateOptions
{
    /// <summary>Marka adı -> takip kodu. Marka adı büyük/küçük harfe duyarsız eşleşir.</summary>
    public Dictionary<string, string> TrackingCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Takip parametresinin adı. SSN ve Hardline'ın ikisi de OpenCart'ın
    /// ortaklık modülünü kullanıyor ve parametre orada "tracking".
    /// Farklı bir altyapı kullanan bir marka eklenirse ayrıştırılabilir.
    /// </summary>
    public string ParameterName { get; set; } = "tracking";
}

/// <summary>
/// Ürün adresine markanın ortaklık takip kodunu ekler.
/// </summary>
public static class AffiliateLinkBuilder
{
    /// <summary>
    /// Kod tanımlıysa adrese takip parametresini ekler, değilse adresi
    /// olduğu gibi döndürür. Adres bozuksa da dokunmaz — yönlendirmenin
    /// çalışması, takip edilmesinden önce gelir.
    /// </summary>
    public static string Apply(string url, string? brandName, AffiliateOptions options)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(brandName))
            return url;

        if (options.TrackingCodes is null ||
            !options.TrackingCodes.TryGetValue(brandName, out var code) ||
            string.IsNullOrWhiteSpace(code))
        {
            return url;
        }

        var parameter = string.IsNullOrWhiteSpace(options.ParameterName) ? "tracking" : options.ParameterName;

        // Adreste zaten aynı parametre varsa ikinci kez eklemiyoruz; marka
        // tarafında hangisinin okunacağı belirsiz olurdu.
        if (url.Contains($"?{parameter}=", StringComparison.OrdinalIgnoreCase) ||
            url.Contains($"&{parameter}=", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var pair = $"{Uri.EscapeDataString(parameter)}={Uri.EscapeDataString(code)}";

        // Adreste sorgu dizesi varsa & ile, yoksa ? ile eklenmeli; yanlış
        // ayraç linki tamamen bozar. Bağlantı çapası (#) varsa parametre
        // ondan ÖNCE gelmeli, yoksa sunucu parametreyi hiç görmez.
        var fragmentIndex = url.IndexOf('#');
        var basePart = fragmentIndex >= 0 ? url[..fragmentIndex] : url;
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;

        var separator = basePart.Contains('?') ? '&' : '?';
        return $"{basePart}{separator}{pair}{fragment}";
    }

    /// <summary>
    /// Adresi Location başlığının taşıyabileceği hale getirir: yalnız ASCII, dışındaki harfler (ü, ş,
    /// ı...) UTF-8 yüzde kodlamasıyla. Mutlak http(s) adresi olmayan her şey olduğu gibi döner.
    /// </summary>
    /// <remarks>
    /// Kestrel ASCII dışı karakterli başlığı reddediyor, yönlendirme 500 ile düşüyordu. 3 Kasım 2026'da
    /// 242 ürünün mağaza adresinde böyle bir harf vardı (209'u Provitamin, ör. "...-90-kapsül"):
    /// "Mağazaya git" bunların hepsinde çalışmıyordu. Şema kontrolü süs değil: Linux'ta "/urun" mutlak
    /// bir file:// adresi olarak ayrıştırılıyor (WheyProof'un CI'ı yakaladı; Windows'ta olmuyor).
    /// </remarks>
    public static string ToLocation(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : url;
}

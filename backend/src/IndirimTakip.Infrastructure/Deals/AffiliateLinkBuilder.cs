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
}

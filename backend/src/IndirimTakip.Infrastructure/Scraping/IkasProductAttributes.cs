using System.Text.Json;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// ikas altyapılı mağazaların ürün sayfasındaki <c>__NEXT_DATA__</c>
/// bloğundan, GEÇERLİ ÜRÜNÜN kendi özellik alanlarını okur.
/// </summary>
/// <remarks>
/// <b>NEDEN "GEÇERLİ ÜRÜNÜN" VURGULANIYOR.</b> Bu sayfalar menü ve öneri
/// listeleriyle birlikte BAŞKA ürünlerin verisini de taşıyor. Grizzone'da
/// ölçüldü: tek bir ürün sayfasında 83 ayrı besin tablosu var ve bunların
/// tamamı menü yükünden geliyor. JSON'da "Besin" geçen ilk değeri almak,
/// başka bir ürünün besin değerlerini bu ürüne yazmak olurdu — uydurma
/// veriden beter, çünkü gerçek ama YANLIŞ ürünün verisi.
///
/// Doğru düğüm <c>props.pageProps.pageSpecificData</c>; sayfanın kendi
/// ürünü orada. (Aynı yapı ProteinOcean'da da kullanılıyor.)
/// </remarks>
internal static partial class IkasProductAttributes
{
    internal readonly record struct Attribute(string Name, string? Type, string? Value);

    public static IReadOnlyList<Attribute> Read(string html)
    {
        var match = NextDataRegex().Match(html);
        if (!match.Success)
            return [];

        try
        {
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            if (!doc.RootElement.TryGetProperty("props", out var props)
                || !props.TryGetProperty("pageProps", out var pageProps)
                || !pageProps.TryGetProperty("pageSpecificData", out var pageData)
                || !pageData.TryGetProperty("attributes", out var attributes)
                || attributes.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var sonuc = new List<Attribute>();
            foreach (var item in attributes.EnumerateArray())
            {
                if (!item.TryGetProperty("productAttribute", out var meta)
                    || !meta.TryGetProperty("name", out var nameEl)
                    || nameEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var type = meta.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                    ? typeEl.GetString()
                    : null;

                // value HTML tipinde dize, TABLE tipinde dizi olabiliyor —
                // yalnızca dize olanı veriyoruz, tablo tipinin çözümü
                // kaynağa özel (bkz. ProteinOceanScraper'daki gerekçe).
                var value = item.TryGetProperty("value", out var valueEl) && valueEl.ValueKind == JsonValueKind.String
                    ? valueEl.GetString()
                    : null;

                sonuc.Add(new Attribute(nameEl.GetString()!, type, value));
            }

            return sonuc;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// GEÇERLİ ÜRÜNÜN açıklama HTML'i (<c>pageSpecificData.description</c>).
    /// </summary>
    /// <remarks>
    /// Bazı ikas mağazaları besin/etken madde tablosunu özellik alanına değil
    /// ürün açıklamasının içine koyuyor (Kiperin, 15 Eylül'de ölçüldü:
    /// <c>attributes</c> dizisinde tablo yok, <c>description</c>'da var).
    /// Aynı düğümden okunuyor, yani menü/öneri yükündeki başka ürünlerin
    /// açıklamaları karışmıyor.
    ///
    /// <b><c>translations[].description</c> BİLEREK OKUNMUYOR.</b> Kiperin'in
    /// kolajen sayfasında aynı tablonun İngilizce kopyası orada duruyor;
    /// Türkçe sitede o değerlerin ve etiketlerin yayınlanması yanlış olurdu.
    /// </remarks>
    public static string? Description(string html)
    {
        var match = NextDataRegex().Match(html);
        if (!match.Success)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            return doc.RootElement.TryGetProperty("props", out var props)
                && props.TryGetProperty("pageProps", out var pageProps)
                && pageProps.TryGetProperty("pageSpecificData", out var pageData)
                && pageData.TryGetProperty("description", out var description)
                && description.ValueKind == JsonValueKind.String
                    ? description.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Adı verilen parçayı içeren ilk alanın değeri.
    /// </summary>
    /// <remarks>
    /// Karşılaştırma Türkçe'ye göre ELLE katlanıyor: kaynak adı "BESİN
    /// DEĞERLERİ" diye büyük harfle de yazabiliyor ve .NET'in invariant
    /// küçültmesi noktalı İ'yi çevirmiyor — <c>OrdinalIgnoreCase</c> ile
    /// "besin" araması o yazımı KAÇIRIRDI. (Grizzone'un kataloğu zaten
    /// tamamen büyük harf, bkz. GrizzoneScraper.)
    /// </remarks>
    public static string? ValueOf(IReadOnlyList<Attribute> attributes, string namePart)
    {
        var aranan = Katla(namePart);

        foreach (var attribute in attributes)
        {
            if (Katla(attribute.Name).Contains(aranan, StringComparison.Ordinal))
                return attribute.Value;
        }

        return null;
    }

    private static string Katla(string text) =>
        text.Replace('İ', 'i').Replace('I', 'ı').ToLowerInvariant();

    [GeneratedRegex(@"__NEXT_DATA__[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();
}

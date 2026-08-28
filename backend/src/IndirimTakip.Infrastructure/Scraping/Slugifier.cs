using System.Text;

namespace IndirimTakip.Infrastructure.Scraping;

// Ürün adından URL parçası üretir. **Frontend'deki `core/slugify.ts` ile
// birebir aynı sonucu vermek ZORUNDA** — ürettiği adres kanonik adresle
// eşleşmezse arama motoruna bildirdiğimiz bağlantı yönlendirmeye düşer ve
// bildirimin değeri kaybolur.
//
// Türkçe harfler elle eşleniyor; kültüre bağlı bir küçültmeye güvenilmiyor.
// Bu projede aynı tuzağa üç kez düşüldü: tr-TR ile büyük "I" noktasız "ı"
// oluyor, ToLowerInvariant ile büyük "İ" hiç küçülmüyor, JavaScript'te
// büyük "İ" küçük "i" ile eşleşmiyor.
public static class Slugifier
{
    // Uzun kombinasyon ürünlerinde adres parçası şişmesin diye.
    private const int MaxSlugLength = 80;

    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var mapped = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            mapped.Append(ch switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                _ => ch,
            });
        }

        // Buradan sonrası yalnızca düz ASCII harflerle ilgileniyor, bu yüzden
        // kültürden bağımsız küçültme güvenli.
        var lowered = mapped.ToString().ToLowerInvariant();

        var slug = new StringBuilder(lowered.Length);
        var lastWasHyphen = true; // baştaki tireleri de engelliyor
        foreach (var ch in lowered)
        {
            if (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9')
            {
                slug.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                slug.Append('-');
                lastWasHyphen = true;
            }
        }

        var result = slug.ToString().Trim('-');
        if (result.Length <= MaxSlugLength) return result;

        var truncated = result[..MaxSlugLength];
        var lastHyphen = truncated.LastIndexOf('-');
        return lastHyphen > 0 ? truncated[..lastHyphen] : truncated;
    }
}

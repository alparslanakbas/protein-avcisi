using System.Globalization;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// "Porsiyon Büyüklüğü: 32g" / "Porsiyon Sayısı: 30 Servis" gibi satırlardan
/// sayıyı çeker.
/// </summary>
/// <remarks>
/// <b>Neden ortak.</b> BigJoy ve Torq aynı bilgiyi neredeyse aynı biçimde
/// veriyor ("32g" ile "30 Gram", "68" ile "30 Servis"). Ayrıştırmayı her
/// scraper'a kopyalamak, aşağıdaki makul-aralık kontrollerinin zamanla
/// birbirinden ayrılması demekti — bu depoda marka takma adları tam olarak
/// böyle bozulmuştu (bkz. d41fc81).
///
/// Aralık kontrolleri BİLİNÇLİ: aralık dışı bir eşleşme, satırın yanlış
/// yakalandığına işaret eder ve uydurma bir porsiyon değeri servis başı
/// fiyat hesabını sessizce kat kat şişirir. Şüpheli değer yerine null.
/// </remarks>
internal static class NutritionServingParser
{
    // Gram: "32g", "30 Gram", "1,5 gr" — sayıdan sonra "g" ile başlayan bir
    // birim gelmeli. Birim şartı önemli: şartsız olsaydı aynı div'deki
    // "Son Kullanma Tarihi: 01/04/2029" satırından 1 çıkardı.
    private static readonly Regex GramPattern =
        new(@"(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CountPattern = new(@"(\d+)", RegexOptions.Compiled);

    /// <summary>Porsiyon büyüklüğü (gram). Makul aralık dışındaysa null.</summary>
    public static decimal? Grams(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = GramPattern.Match(text);
        if (!match.Success)
            return null;

        return decimal.TryParse(
                match.Groups[1].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var grams)
            && grams > 0 && grams <= 500
            ? grams
            : null;
    }

    /// <summary>Paketteki servis sayısı. Makul aralık dışındaysa null.</summary>
    public static int? Count(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = CountPattern.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) && count is > 0 and <= 1000
            ? count
            : null;
    }
}

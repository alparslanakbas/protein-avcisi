using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping.NutritionLabels;

/// <summary>
/// Türkçe besin etiketinin OCR metninden porsiyon başına makroları çıkarır —
/// yalnızca etiket KENDİ KENDİNİ doğruluyorsa.
/// </summary>
/// <remarks>
/// <b>ÖLÇÜM (15 Eylül, Nois'in 55 etiketi, tesseract 5.3.4 tur+eng).</b>
/// OCR hataları rastgele değil, sistematik: birimsiz kalan "g" → "9"
/// ("2,75g" → "2,759", "24,9 g" → "24.99"), kaybolan virgül ("0,5g" →
/// "05g", "2,30 g" → "230g") ve "0" → "O". Düzeltmeler aşağıda; ama asıl
/// güvence düzeltme değil, KONTROL.
///
/// <b>Satır kontrolü (asıl güvence).</b> Türkçe etiket her makroyu İKİ kez
/// basıyor: 100 g'da ve porsiyonda. Porsiyon değeri "100 g değeri × porsiyon
/// / 100" etmek zorunda. Bir basamak yanlış okunursa oran bozulur ve satır
/// reddedilir — "230g" (gerçekte 2,30) ile 7,66 g/100 g tutmaz. Kalori
/// kontrolü küçük porsiyonda tek başına zayıf kalıyordu: EAA'da 5 kcal'lik
/// porsiyonda "0,3g" → "3g" okunsa ±15 kcal toleransın içinde kalırdı.
///
/// <b>Kalori kontrolü (ikinci güvence).</b> 4×protein + 4×karbonhidrat +
/// 9×yağ (+ lif için 0-2 kcal/g; AB etiketinde lif karbonhidrata dahil değil).
///
/// <b>Reddedilen doğru şeyler de var ve bu kabul edildi.</b> Vegan Rex'in
/// etiketi 100 g'da "1406,06 kcal" basıyor (gerçekte ~406); Promeal başlığı
/// "1 Porsiyon(60 G)" diyor ama değerler 100 g'ın tam yarısı; Testo Booster
/// "1000 Gr"da 40 kcal, 10 g porsiyonda 4 kcal diyor. Etiketin kendisi
/// tutarsızsa hangi sayının doğru olduğunu bilemeyiz; boş kalıyor.
///
/// <b>Sütun sırası başlıktan okunuyor.</b> Nois Electrolyte "1 Servis (3 g)
/// | 100 g" basıyor, diğerleri "100 G İçin | 1 Porsiyon". Sırayı varsaymak o
/// üründe 100 g değerlerini porsiyon diye yazardı.
/// </remarks>
internal static partial class TurkishLabelTextParser
{
    internal sealed record Result(IReadOnlyList<(string Label, string Value)> Rows, decimal ServingGrams);

    private enum Kind { Energy, Fat, Saturated, Carbs, Sugar, Fiber, Protein, Salt }

    // Sıra önemli: "Doymuş Yağ" "Yağ"dan önce denenmeli. Yayın sırası da bu.
    private static readonly (Kind Kind, string Label, Regex Pattern)[] Labels =
    [
        (Kind.Energy, "Enerji", new(@"^(enerji|energy)", RegexOptions.CultureInvariant)),
        (Kind.Saturated, "Doymuş Yağ", new(@"^doymu", RegexOptions.CultureInvariant)),
        (Kind.Fat, "Yağ", new(@"^(toplam\s+)?ya[gz]\b", RegexOptions.CultureInvariant)),
        (Kind.Carbs, "Karbonhidrat", new(@"^karbonhidrat", RegexOptions.CultureInvariant)),
        (Kind.Sugar, "Şeker", new(@"^seker", RegexOptions.CultureInvariant)),
        (Kind.Fiber, "Lif", new(@"^(diyet\s+)?lif", RegexOptions.CultureInvariant)),
        (Kind.Protein, "Protein", new(@"^protein\b", RegexOptions.CultureInvariant)),
        (Kind.Salt, "Tuz", new(@"^tuz\b", RegexOptions.CultureInvariant)),
    ];

    private static readonly Kind[] PublishOrder =
        [Kind.Energy, Kind.Fat, Kind.Saturated, Kind.Carbs, Kind.Sugar, Kind.Fiber, Kind.Protein, Kind.Salt];

    private static readonly Kind[] Required = [Kind.Energy, Kind.Protein, Kind.Carbs, Kind.Fat];

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static Result? Parse(string ocrText)
    {
        var lines = ocrText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var headerIndex = Array.FindIndex(lines, IsHeader);
        if (headerIndex < 0)
            return null;

        var header = Fold(lines[headerIndex]);
        var baseMatch = BaseAmountRegex().Match(header);
        var portionMatch = PortionWordRegex().Match(header);
        var baseFirst = baseMatch.Index < portionMatch.Index;
        var baseAmount = decimal.Parse(baseMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        var serving = ServingInParenthesesRegex().Match(header) is { Success: true } inHeader
            ? ParseNumber(inHeader.Groups[1].Value)
            : ServingAbove(lines, headerIndex);
        if (serving is not > 0 || serving > 500)
            return null;

        var ratio = serving.Value / baseAmount;
        var accepted = new Dictionary<Kind, Amount>();

        foreach (var raw in lines.Skip(headerIndex + 1))
        {
            var folded = Fold(raw).TrimStart('-', '=', '*', '“', '"', '\'', ' ', '_', '.', '(');
            var match = Labels.FirstOrDefault(l => l.Pattern.IsMatch(folded));
            if (match.Pattern is null || accepted.ContainsKey(match.Kind))
                continue;

            var values = match.Kind == Kind.Energy ? Kcal(raw) : Masses(raw);
            if (values.Count != 2 || values[0] is not { } first || values[1] is not { } second)
                continue;

            var (portion, per100) = baseFirst ? (second, first) : (first, second);
            if (RowConsistent(portion, per100, ratio))
                accepted[match.Kind] = portion;
        }

        if (Required.Any(k => !accepted.ContainsKey(k)) || !CaloriesConsistent(accepted))
            return null;

        var rows = PublishOrder
            .Where(accepted.ContainsKey)
            .Select(k => (Labels.First(l => l.Kind == k).Label, accepted[k].Display))
            .ToList();

        return new Result(rows, serving.Value);
    }

    private static bool IsHeader(string line)
    {
        var folded = Fold(line);
        return BaseAmountRegex().IsMatch(folded) && PortionWordRegex().IsMatch(folded);
    }

    // "Porsiyon Miktarı: 30 Gr", "1 Servis = 60 g", "Servis Miktarı :(3 g)".
    // "Servis Sayısı: 30" gram taşımıyor, eşleşmez.
    private static decimal? ServingAbove(string[] lines, int headerIndex)
    {
        for (var i = headerIndex - 1; i >= 0; i--)
        {
            var folded = Fold(lines[i]);
            if (PortionWordRegex().IsMatch(folded) && GramAmountRegex().Match(folded) is { Success: true } m)
                return ParseNumber(m.Groups[1].Value);
        }

        return null;
    }

    /// <param name="Grams">Hesapta kullanılan değer (gram; enerjide kcal).</param>
    /// <param name="Decimals">Değerin gram ölçeğinde basıldığı basamak — yuvarlama payı için.</param>
    /// <param name="Display">Etikette basıldığı birim ve basamakla gösterim ("0,975 g", "1.277,25 mg").</param>
    private readonly record struct Amount(decimal Grams, int Decimals, string Display);

    private static List<Amount?> Kcal(string raw) =>
        KcalRegex().Matches(NormalizeZeros(raw))
            .Select(m => ToAmount(m.Groups[1].Value, "kcal"))
            .ToList();

    private static List<Amount?> Masses(string raw)
    {
        var text = NormalizeZeros(raw);

        // "2,759" → "2,75g": birimi olmayan ondalık sayının sondaki 9'u
        // okunamamış "g". Yalnızca birimsiz sayıya uygulanıyor; "0,59g" gibi
        // birimi okunmuş değere dokunmuyor.
        text = TrailingNineRegex().Replace(text, "$1g");

        return MassTokenRegex().Matches(text)
            .Select(m => ToAmount(FixLostComma(m.Groups[1].Value), m.Groups[2].Value.ToLowerInvariant()))
            .ToList();
    }

    // "Og", "O gr", "O Kcal(Okj)" → 0. Harfin kelime içinde olmadığı yerde.
    private static string NormalizeZeros(string raw) => LoneLetterORegex().Replace(raw, "0");

    // "05g" → "0,5", "028g" → "0,28": etikette baştaki sıfırdan sonra her
    // zaman virgül var, OCR onu kaybediyor.
    private static string FixLostComma(string number) =>
        number.Length >= 2 && number[0] == '0' && char.IsDigit(number[1])
            ? "0," + number[1..]
            : number;

    private static Amount? ToAmount(string token, string unit)
    {
        if (ParseNumber(token) is not { } value)
            return null;

        var decimals = DecimalsOf(token);
        var display = value.ToString("N" + decimals, Turkish) + " " + (unit == "gr" ? "g" : unit);

        // "12.772,5mg" karbonhidrat: BCAA etiketi makroyu miligramla basıyor.
        return unit == "mg"
            ? new Amount(value / 1000m, decimals + 3, display)
            : new Amount(value, decimals, display);
    }

    private static int DecimalsOf(string token)
    {
        // Binlik noktası ("8.333,33") ondalık sayılmıyor.
        var separator = token.Contains(',') ? token.LastIndexOf(',') : token.LastIndexOf('.');
        return separator < 0 ? 0 : token.Length - separator - 1;
    }

    private static bool RowConsistent(Amount portion, Amount per100, decimal ratio)
    {
        var expected = per100.Grams * ratio;
        // Etiketin kendi yuvarlaması: her iki sayı da basıldığı basamakta
        // yuvarlanmış. "53 g" (±0,5) × 0,3 ile "16 g" (±0,5) yan yana tutarlı.
        var rounding = HalfUnit(portion.Decimals) + HalfUnit(per100.Decimals) * ratio;
        var tolerance = Math.Max(rounding, expected * 0.03m) + 0.01m;
        return Math.Abs(portion.Grams - expected) <= tolerance;
    }

    private static decimal HalfUnit(int decimals)
    {
        var half = 0.5m;
        for (var i = 0; i < decimals; i++)
            half /= 10;
        return half;
    }

    private static bool CaloriesConsistent(Dictionary<Kind, Amount> v)
    {
        var baseline = 4 * v[Kind.Protein].Grams + 4 * v[Kind.Carbs].Grams + 9 * v[Kind.Fat].Grams;
        var fiber = v.TryGetValue(Kind.Fiber, out var f) ? f.Grams : 0;
        var kcal = v[Kind.Energy].Grams;
        var tolerance = Math.Max(3m, kcal * 0.1m);
        return kcal >= baseline - tolerance && kcal <= baseline + 2 * fiber + tolerance;
    }

    private static decimal? ParseNumber(string token)
    {
        var t = token.Trim().TrimEnd('.', ',');
        // "8.333,33" → nokta binlik; tek başına nokta ya da virgül ondalık.
        if (t.Contains('.') && t.Contains(','))
            t = t.Replace(".", "");
        t = t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    // Türkçe harfler ELLE katlanıyor; OCR noktalı/noktasız harfi de sık
    // karıştırıyor ("Yag", "Seker"), eşleştirme harf işaretsiz yapılıyor.
    private static string Fold(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                'İ' or 'I' or 'ı' => 'i',
                'Ğ' or 'ğ' => 'g',
                'Ş' or 'ş' => 's',
                'Ö' or 'ö' => 'o',
                'Ü' or 'ü' => 'u',
                'Ç' or 'ç' => 'c',
                _ => char.ToLowerInvariant(c),
            });
        }

        return sb.ToString();
    }

    // "100g", "100 G İçin", "1000 Gr Üründe". "100 mg" eşleşmez (m araya giriyor).
    [GeneratedRegex(@"(?<![\d.,])(100|1000)\s*gr?(?![a-z]*\s*\))", RegexOptions.CultureInvariant)]
    private static partial Regex BaseAmountRegex();

    [GeneratedRegex(@"porsiyon|servis", RegexOptions.CultureInvariant)]
    private static partial Regex PortionWordRegex();

    // "(~30g)", "(3 g)", "(60 G)", "(10 Gr)".
    [GeneratedRegex(@"\(\s*~?\s*(\d+(?:[.,]\d+)?)\s*gr?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ServingInParenthesesRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*gr?\b", RegexOptions.CultureInvariant)]
    private static partial Regex GramAmountRegex();

    [GeneratedRegex(@"(\d[\d.,]*)\s*kcal", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KcalRegex();

    // Birimsiz, ondalıklı ve 9 ile biten sayı; ardından birim ya da % gelmiyor.
    [GeneratedRegex(@"(?<![\d.,])(\d+[.,]\d*)9(?=\s|\||$)(?!\s*(?:%|g|gr|mg|kcal|kj)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNineRegex();

    // Kütle birimli sayı; ardından yüzde işareti gelen sayı DV sütunu, alınmıyor.
    [GeneratedRegex(@"(?<![\d.,%])(\d[\d.,]*)\s*(mg|gr|g)\b(?!\s*%)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MassTokenRegex();

    // Kelimenin parçası olmayan O: "Og", "O gr", "O Kcal(Okj)", "(Okj)".
    [GeneratedRegex(@"(?<![A-Za-zÇĞİÖŞÜçğıöşü])O(?=\s*(?:g|gr|kcal|kj|%)\b|kj)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoneLetterORegex();
}

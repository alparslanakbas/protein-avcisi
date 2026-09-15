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

            var columns = match.Kind == Kind.Energy ? Kcal(raw) : Masses(raw);
            if (columns is not [var first, var second])
                continue;

            var (portions, per100s) = baseFirst ? (second, first) : (first, second);

            // Her sütunun olası okumaları çaprazlanıyor; oranı tutan porsiyon
            // değerleri toplanıyor. TEK bir değer kalırsa kabul, farklı iki
            // değer tutuyorsa hangisinin doğru olduğu bilinemez: ret.
            var consistent = (
                from portion in portions
                from per100 in per100s
                where RowConsistent(portion, per100, ratio)
                select portion).ToList();

            if (consistent.Count > 0 && consistent.All(p => p.Grams == consistent[0].Grams))
                accepted[match.Kind] = consistent[0];
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
    // "Servis Sayısı: 30" gram taşımıyor, eşleşmez. West etiketi porsiyonu
    // iki dilde basıyor; Türkçe satır "(30g)" yerine "(0g)" okunduğunda
    // İngilizce "Serving size ... (30g)" satırı kullanılıyor, sıfır atlanıyor.
    private static decimal? ServingAbove(string[] lines, int headerIndex)
    {
        for (var i = headerIndex - 1; i >= 0; i--)
        {
            var folded = Fold(lines[i]);
            if (!PortionWordRegex().IsMatch(folded) && !folded.Contains("serving", StringComparison.Ordinal))
                continue;

            foreach (Match m in GramAmountRegex().Matches(folded))
            {
                if (ParseNumber(m.Groups[1].Value) is > 0 and var grams)
                    return grams;
            }
        }

        return null;
    }

    /// <param name="Grams">Hesapta kullanılan değer (gram; enerjide kcal).</param>
    /// <param name="Decimals">Değerin gram ölçeğinde basıldığı basamak — yuvarlama payı için.</param>
    /// <param name="Display">Etikette basıldığı birim ve basamakla gösterim ("0,975 g", "1.277,25 mg").</param>
    private readonly record struct Amount(decimal Grams, int Decimals, string Display);

    // Her sütun için OLASI okumalar. Enerjide belirsizlik yok: kcal birimi okunmuş.
    private static List<List<Amount>> Kcal(string raw) =>
        KcalRegex().Matches(NormalizeZeros(raw))
            .Select(m => ToAmount(m.Groups[1].Value, "kcal") is { } a ? new List<Amount> { a } : [])
            .ToList();

    /// <summary>Satırdaki iki kütle sütununun olası okumaları; iki sütun bulunamazsa boş.</summary>
    /// <remarks>
    /// <b>Neden tek okuma değil.</b> Birimi okunamamış ve 9 ile biten sayı iki
    /// şey olabilir: "2,759" "2,75 g" demek, ama "29" hem "2 g" hem gerçek 29
    /// olabilir. West etiketinde ölçüldü: "Yağ / Fat 6,8 g 29" (gerçekte 2 g),
    /// "Doymuş Yağ 4g 1,29" (1,2 g), "Protein 73,8 9 22" (73,8 g, 22 g). Tek
    /// kurallı düzeltme bunlardan birini mutlaka yanlış çözerdi. İki okuma
    /// üretilip hangisinin satır kontrolünü geçtiğine bakılıyor.
    ///
    /// <b>Hangi sayılar sütun.</b> Önce birimi okunmuş sayılar; tam iki tane
    /// değilse birimsizler de katılıyor. Yüzde işaretli sayı (DV/BRD) hiç
    /// alınmıyor. Nois'te "Tuz 0,97g 0,04 | 0,29g 0,01" gibi yüzde işareti
    /// düşmüş DV sütunları var; birimli iki sayı zaten bulunduğu için karışmıyor.
    /// </remarks>
    private static List<List<Amount>> Masses(string raw)
    {
        // "10,7 9g" / "73,8 9": boşlukla ayrılmış tek başına 9, okunamamış "g".
        var text = SpacedNineRegex().Replace(NormalizeZeros(raw), "$1g");

        var tokens = NumberTokenRegex().Matches(text)
            .Select(m => (Number: m.Groups[1].Value, Unit: m.Groups[2].Value.ToLowerInvariant()))
            .ToList();

        // "0,/g" gibi basamağı kaybolmuş, ayraçla biten sayı KESİK okuma. 0
        // sayılsaydı tam sayının ±0,5 yuvarlama payını alır ve porsiyondaki
        // her küçük değerle "tutarlı" çıkardı (West tuz satırında görüldü).
        // Satır kullanılmıyor.
        if (tokens.Any(t => t.Number.EndsWith(',') || t.Number.EndsWith('.')))
            return [];

        // Sırayla: birimi okunmuşlar; birimli ya da 9 ile bitenler (sondaki 9
        // okunamamış "g" olabilir — Nois'te yüzde işareti düşmüş DV sütunu
        // "13,69 4,95% | 0,68g 0,24" satırında üçüncü birimsiz sayı oluyor);
        // en son hepsi. Tam iki sütun veren ilk küme kullanılıyor.
        var withUnit = tokens.Where(t => t.Unit.Length > 0).ToList();
        var gramCandidates = tokens.Where(t => t.Unit.Length > 0 || t.Number.EndsWith('9')).ToList();
        var chosen = withUnit.Count == 2 ? withUnit
            : gramCandidates.Count == 2 ? gramCandidates
            : tokens.Count == 2 ? tokens
            : null;
        if (chosen is null)
            return [];

        return chosen.Select(t => Readings(t.Number, t.Unit)).ToList();
    }

    private static List<Amount> Readings(string number, string unit)
    {
        var readings = new List<Amount>();
        if (ToAmount(FixLostComma(number), unit.Length == 0 ? "g" : unit) is { } asPrinted)
            readings.Add(asPrinted);

        // Birimsiz ve 9 ile biten: sondaki 9 okunamamış "g" olabilir.
        if (unit.Length == 0 && number.Length >= 2 && number[^1] == '9'
            && ToAmount(FixLostComma(number[..^1].TrimEnd(',', '.')), "g") is { } withoutNine)
        {
            readings.Add(withoutNine);
        }

        return readings;
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

    // Ondalıklı sayıdan sonra boşlukla gelen tek başına 9 (ardından g olabilir).
    [GeneratedRegex(@"(\d+[.,]\d+)\s+9g?(?=\s|\||$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpacedNineRegex();

    // Sayı ve (varsa) kütle birimi. Yüzde işaretinin önündeki ya da ardındaki
    // sayı DV/BRD sütunu, alınmıyor. Harfe bitişik sayı ("B6", "4:1:1") da değil.
    [GeneratedRegex(@"(?<![\d.,%:A-Za-z])(\d[\d.,]*)\s*(mg|gr|g)?(?![\d.,:A-Za-z]|\s*%)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberTokenRegex();

    // Kelimenin parçası olmayan O: "Og", "O gr", "O Kcal(Okj)", "(Okj)".
    [GeneratedRegex(@"(?<![A-Za-zÇĞİÖŞÜçğıöşü])O(?=\s*(?:g|gr|kcal|kj|%)\b|kj)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoneLetterORegex();
}

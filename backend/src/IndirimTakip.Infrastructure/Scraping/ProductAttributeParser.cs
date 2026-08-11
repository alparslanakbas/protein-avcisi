using System.Globalization;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

// Marka scraper'ları ürün ismini olduğu gibi veriyor (örn. "SSN ... 2100 Gr
// (Bisküvi) Protein Tozu"). Boyut, aroma ve (eksikse) kategori bilgisini bu
// tek isimden çıkarıyoruz — böylece 4 farklı sitede ayrı ayrı parse mantığı
// yazmak yerine tüm markalar için tek bir yerde çözülüyor. Ayrıca arama
// kutusunun markadan bağımsız çalışması da buna dayanıyor (bkz. Category).
public static partial class ProductAttributeParser
{
    private static readonly Dictionary<string, string> UnitCanonical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = "Gr",
        ["gr"] = "Gr",
        ["kg"] = "Kg",
        ["mg"] = "Mg",
        ["ml"] = "Ml",
        ["lt"] = "Lt",
        ["l"] = "Lt",
        ["adet"] = "Adet",
        ["tablet"] = "Tablet",
        ["kapsul"] = "Kapsül",
        ["kapsül"] = "Kapsül",
        ["kaps"] = "Kapsül",
        ["caps"] = "Kapsül",
        ["softjel"] = "Softjel",
        ["sase"] = "Şase",
        ["şase"] = "Şase",
    };

    // Kategori tahmini için anahtar kelimeler — SSN'in kendi kategori
    // slug'larıyla tutarlı olacak şekilde adlandırıldı.
    private static readonly (string Category, string[] Keywords)[] CategoryKeywords =
    [
        ("protein-tozu", ["protein", "whey", "isolate", "izole", "casein", "kazein"]),
        ("kreatin", ["creatine", "kreatin"]),
        ("amino-asitler", ["amino", "bcaa", "eaa", "glutamin", "arginin", "arjinin", "sitrulin", "citrulline"]),
        ("pre-workout", ["pre workout", "preworkout", "pump", "nitric", "hellfire", "pre-workout"]),
        ("yag-yakici", ["burner", "yag yakici", "thermo", "l-carnitine", "karnitin", "carnitine", "cla"]),
        ("kilo-hacim", ["gainer", "mass", "kilo", "hacim"]),
        ("vitamin", ["vitamin", "mineral", "magnesium", "magnezyum", "zinc", "cinko", "omega", "multivitamin"]),
        ("saglikli-atistirmaliklar", ["bar", "cookie", "kurabiye", "atistirmalik", "rice cake", "pirinc"]),
    ];

    public static string? ExtractSize(string productName)
    {
        var match = SizeRegex().Match(productName);
        if (!match.Success)
            return null;

        var value = match.Groups["value"].Value.Replace(',', '.');
        var unitKey = match.Groups["unit"].Value.ToLowerInvariant();
        var unit = UnitCanonical.GetValueOrDefault(unitKey, unitKey);
        return $"{value} {unit}";
    }

    public static string? ExtractFlavor(string productName)
    {
        foreach (Match match in ParenthesesRegex().Matches(productName))
        {
            var content = match.Groups[1].Value.Trim();
            // Boyut bilgisini taşıyan parantezleri ("60 Ml *18 Adet" gibi) aroma sanmayalım.
            if (content.Length == 0 || SizeRegex().IsMatch(content))
                continue;

            return content;
        }

        return null;
    }

    public static string? InferCategory(string productName)
    {
        var normalized = productName.ToLower(new CultureInfo("tr-TR"));

        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
                return category;
        }

        return null;
    }

    [GeneratedRegex(@"(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>gr|g|kg|mg|ml|lt|l|adet|tablet|kaps[uü]l|kaps|caps|softjel|[şs]ase)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex ParenthesesRegex();
}

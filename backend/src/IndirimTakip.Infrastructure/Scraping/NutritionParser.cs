using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

// Besin değeri tabloları 4 markada da farklı HTML yapılarında geliyor
// (Hardline'da div satırları, HIQ'da <table>, SSN'de açıklama içindeki tablo,
// ProteinOcean'da __NEXT_DATA__ içinde HTML). Her scraper kendi yapısından
// ham (etiket, değer) çiftlerini çıkarıyor; normalize etme, JSON'a çevirme ve
// protein değerini ayrıştırma işi burada — tek yerde — yapılıyor.
internal static class NutritionParser
{
    // Bir besin tablosu satırında makul kabul edilen en fazla etiket uzunluğu.
    // Bundan uzun "etiketler" genellikle tablo değil, araya karışmış bir
    // açıklama paragrafıdır — tabloya alınmıyor.
    private const int MaxLabelLength = 60;
    private const int MaxValueLength = 40;

    // Hardline "Protein / Protein" gibi Türkçe/İngilizce ikili etiket
    // kullanıyor — ilk parça yeterli, ikinci parça tekrar.
    private static readonly Regex DuplicateLabelPattern = new(@"^(.+?)\s*/\s*(.+)$", RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    // "24 g", "24,5g", "1.049 mg", "120 kcal" gibi değerlerden sayıyı çeker.
    private static readonly Regex NumberPattern = new(@"(\d+(?:[.,]\d+)?)", RegexOptions.Compiled);

    // Protein satırının etiketi marka/dile göre değişiyor: "Protein",
    // "Protein / Protein", "Protein (g)", "Toplam Protein"... Hepsi "protein"
    // kelimesini içeriyor ama "Protein Tozu" gibi ürün adı satırlarını
    // dışarıda tutmak için ek kelimeleri eliyoruz.
    private static readonly string[] ProteinExclusions = ["tozu", "kaynak", "source", "blend", "matrix"];

    // Ham (etiket, değer) çiftlerinden normalize edilmiş bir besin tablosu
    // kurar. Anlamlı hiç satır yoksa null döner — boş bir tablo saklamak
    // yerine "veri yok" demeyi tercih ediyoruz.
    public static string? BuildNutritionJson(IEnumerable<(string Label, string Value)> rows)
    {
        var table = new Dictionary<string, string>();

        foreach (var (rawLabel, rawValue) in rows)
        {
            var label = NormalizeLabel(rawLabel);
            var value = Normalize(rawValue);

            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value))
                continue;
            if (label.Length > MaxLabelLength || value.Length > MaxValueLength)
                continue;
            // Değer içinde en az bir sayı olmalı — "Ürün Açıklaması: ..." gibi
            // tabloya karışan metin satırlarını eliyor.
            if (!NumberPattern.IsMatch(value))
                continue;

            // Aynı etiket iki kez geçerse ilki korunuyor (tablolarda ikinci
            // sütun genelde "%RDA" gibi ikincil bir değer oluyor).
            table.TryAdd(label, value);
        }

        return table.Count > 0 ? JsonSerializer.Serialize(table) : null;
    }

    // Normalize edilmiş tablodan porsiyon başı protein (gram) çeker.
    // Bulunamazsa null — tahmin üretilmiyor.
    public static decimal? ExtractProteinGrams(string? nutritionJson)
    {
        if (string.IsNullOrEmpty(nutritionJson))
            return null;

        Dictionary<string, string>? table;
        try
        {
            table = JsonSerializer.Deserialize<Dictionary<string, string>>(nutritionJson);
        }
        catch (JsonException)
        {
            return null;
        }

        if (table is null)
            return null;

        foreach (var (label, value) in table)
        {
            var lowered = label.Replace('İ', 'i').ToLowerInvariant();
            if (!lowered.Contains("protein"))
                continue;
            if (ProteinExclusions.Any(lowered.Contains))
                continue;

            // Değer gram cinsinden olmalı — "mg" ya da "kcal" ise bu satır
            // protein miktarı değil (ör. "Proteinden gelen kalori").
            var loweredValue = value.ToLowerInvariant();
            if (loweredValue.Contains("kcal") || loweredValue.Contains("kj") || loweredValue.Contains("mg"))
                continue;

            var match = NumberPattern.Match(value);
            if (!match.Success)
                continue;

            if (decimal.TryParse(
                    match.Groups[1].Value.Replace(',', '.'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var grams)
                // Porsiyon başı protein için makul aralık — bunun dışındaki
                // eşleşmeler yanlış satır yakalandığına işaret eder.
                && grams > 0 && grams <= 100)
            {
                return grams;
            }
        }

        return null;
    }

    private static string NormalizeLabel(string rawLabel)
    {
        var label = Normalize(rawLabel).TrimEnd(':', '.');

        // "Protein / Protein" → "Protein" (aynı kelimenin tekrarıysa).
        var duplicate = DuplicateLabelPattern.Match(label);
        if (duplicate.Success)
        {
            var first = duplicate.Groups[1].Value.Trim();
            var second = duplicate.Groups[2].Value.Trim();
            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
                return first;
        }

        return label;
    }

    private static string Normalize(string raw) =>
        WhitespacePattern.Replace(raw.Replace("&nbsp;", " ").Replace(' ', ' '), " ").Trim();
}

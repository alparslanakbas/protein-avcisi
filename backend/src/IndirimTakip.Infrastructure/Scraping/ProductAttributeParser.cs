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
        // "collagen"/"hipro"/"high pro" eklendi (2026-08-17 kapsamlı kategori
        // taraması) — üçü de gerçek protein ürünleri, HIQ/Hardline'da hiç
        // yakalanmıyordu. "high pro" araya boşluklu yazıldığı için "hipro"
        // (Hardline'ın bitişik yazımı) onu yakalamıyordu, ayrıca eklendi.
        // "creapure"/"glutapure" markalı ama şeffaf isimler (Creapure = saf
        // kreatin monohidrat, Glutapure = Hardline'ın glutamin ürün adı) —
        // gerçek bileşeni doğrudan taşıyor, tahmin değil.
        ("protein-tozu", ["protein", "whey", "isolate", "izole", "casein", "kazein", "collagen", "hipro", "high pro"]),
        ("kreatin", ["creatine", "kreatin", "creapure"]),
        // Aynı taramada eklenen tekil amino asitler — "amino" kelimesi geçmeyen
        // (ör. sadece "Glycine", "Taurine" yazan) ürünler hiç yakalanmıyordu.
        ("amino-asitler", ["amino", "bcaa", "eaa", "glutamin", "arginin", "arjinin", "sitrulin", "citrulline", "alanine", "alanin", "glycine", "taurine", "theanine", "tyrosine", "leucine", "glutapure"]),
        // "caffeine" eklendi: hem HIQ/Hardline/ProteinOcean'da tek başına
        // satılan kafein ürünleri var, enerji/odaklanma amaçlı pre-workout
        // ailesine en yakın kategori bu.
        ("pre-workout", ["pre workout", "preworkout", "pump", "nitric", "hellfire", "pre-workout", "caffeine"]),
        // SSN kendi ürünlerinde bu kategoriyi doğrudan veriyor (elle set edilmiş
        // slug); diğer markalarda (HIQ/Hardline/ProteinOcean) daha önce burada
        // hiç bir giriş olmadığı için l-carnitine/karnitin/cla ürünleri yanlışlıkla
        // "yag-yakici"nin anahtar kelime listesine düşüp o kategoriye gidiyordu —
        // kullanıcı L-Carnitine kategori sayfasında sadece SSN görünce fark etti.
        // "alcar" (Acetyl L-Carnitine'in sektörde standart kısaltması) ve
        // Hardline'ın "Carnifit"/"Carnıfıt" (Carni+Fit) ürün adı da eklendi.
        ("l-carnitine-cla", ["l-carnitine", "karnitin", "carnitine", "cla", "alcar", "carnifit", "carnıfıt"]),
        // "termojenik" (bundle paket adında geçiyor, kelimenin kendisi zaten
        // "yağ yakıcı/termojenik" anlamına geliyor) eklendi.
        ("yag-yakici", ["burner", "yag yakici", "thermo", "termojenik"]),
        // "gain" ayrı eklendi: "gainer" ile eşleşmeyen "HIQ Gain Deluxe" gibi
        // ürünler var. Karbonhidrat/kütle kaynakları da (maltodextrin,
        // dextrose, Vitargo, Cream of Rice, Carbopure) bu kategoriye eklendi.
        ("kilo-hacim", ["gainer", "gain", "mass", "kilo", "hacim", "maltodextrin", "dextrose", "vitargo", "cream of rice", "carbopure"]),
        // Kapsamlı kategori taraması (2026-08-17): vitamin/mineral kategorisinin
        // kendi açıklaması zaten geniş bir yelpaze tanımlıyor ("multivitaminden
        // omega-3'e, magnezyumdan çinkoya") — bu ruhla, önceden hiç bir kategoriye
        // düşmeyen ama gerçek, tanınabilir sağlık takviyesi bileşenleri eklendi.
        // "glucoflex" (Glucosamine+Flex, HIQ'nun eklem sağlığı ürünü, mevcut
        // glucosamine ailesiyle aynı yerde), "curcumin" (zerdeçal özütü) ve
        // "spirulina" (tanınmış bir süperfood takviyesi) de eklendi.
        // Markalı/özel karışım isimleri (GH-UP, Smash Pro, T-Prime vb.) BİLİNÇLİ
        // OLARAK eklenmedi — isimden çıkarım değil tahmin olurdu.
        ("vitamin", ["vitamin", "mineral", "magnesium", "magnezyum", "zinc", "cinko", "omega", "multivitamin", "biotin", "coenzyme", "ginkgo", "glutathione", "hyaluronic", "inulin", "milk thistle", "panax", "ginseng", "psyllium", "rhodiola", "saw palmetto", "selenium", "tribulus", "zma", "glucosamine", "chondroitin", "nmn", "tudca", "ester-c", "5-htp", "b-complex", "lion's mane", "maca", "iron", "chromium", "glucoflex", "curcumin", "spirulina"]),
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
        // ToLowerInvariant bilinçli — tr-TR kültüründe büyük "I" küçülünce
        // noktasız "ı" oluyor ("CREATINE" -> "creatıne"), bu da aşağıdaki
        // İngilizce anahtar kelimelerle ("creatine" gibi) hiç eşleşmiyordu.
        // İkinci, ayrı bir tuzak daha var: Türkçe büyük noktalı "İ"
        // (ör. "C VİTAMİNİ") ToLowerInvariant ile HİÇ küçülmüyor (invariant
        // kültürde bu harf için basit bir eşleme yok) — "vİtamİnİ" olarak
        // kalıp "vitamin" anahtar kelimesiyle asla eşleşmiyordu (canlı veride
        // yüzlerce ürünün kategorisiz kalmasının gerçek sebeplerinden biriydi).
        // Elle .Replace ile düzeltiliyor, culture-sensitive ToLower'a dönmeden.
        var normalized = productName.Replace('İ', 'i').ToLowerInvariant();

        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
                return category;
        }

        return null;
    }

    // Arama kutusu için: kullanıcı "kreatin" yazdığında "creatine" geçen
    // ürünleri de (ve tersini) bulabilsin diye kategori tahmininde zaten
    // var olan Türkçe/İngilizce eşanlamlı kelime gruplarını yeniden
    // kullanıyoruz — ayrı bir eşanlamlı sözlük bakımı gerekmiyor.
    public static IReadOnlyCollection<string> GetSearchSynonyms(string term)
    {
        foreach (var (_, keywords) in CategoryKeywords)
        {
            if (keywords.Any(keyword => keyword.Contains(term, StringComparison.Ordinal) || term.Contains(keyword, StringComparison.Ordinal)))
                return keywords;
        }

        return [];
    }

    // Markanın kendi ürün açıklamasından porsiyon (servis) büyüklüğünü
    // çıkarır. HIQ'da bu bilgi zaten yapısal olarak (Shopify'ın besin değeri
    // tablosundan) geliyordu ama diğer 3 markada hiç yoktu — açıklamalar
    // çekilmeye başlandıktan sonra bu bilginin metnin içinde ("1 ölçek (30 g)",
    // "Porsiyon Büyüklüğü: 25 g", "Servis başına 23 g" gibi) serbest formda
    // durduğu görüldü. Dört farklı yazım kalıbı deneniyor; hiçbiri tutmazsa
    // null dönüyor (tahmin/varsayım YOK — "30 gr = 1 servis" gibi bir kabul
    // bu projede bilinçli olarak hiç yapılmadı).
    public static decimal? ExtractServingSizeGrams(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        foreach (var regex in ServingSizeRegexes)
        {
            var match = regex().Match(description);
            if (!match.Success)
                continue;

            if (!decimal.TryParse(
                    match.Groups["value"].Value.Replace(',', '.'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var grams))
            {
                continue;
            }

            // Makul olmayan eşleşmeleri ele: gerçek veride en küçük porsiyonlar
            // tekil amino asitlerde 1 g (Citrulline/Glycine), en büyükleri
            // gainer'larda 200 g civarı. Bunun dışına taşan bir sayı, metinde
            // porsiyonla ilgisiz bir yerden yakalanmış demektir (ör. bir sos
            // ürününde 0,22 g).
            if (grams is >= 1m and <= 500m)
                return grams;
        }

        return null;
    }

    // Sıra önemli: en açık/az yanılabilir kalıptan başlıyor ("Porsiyon
    // Büyüklüğü: 30 g"), en sonda daha gevşek olan geliyor.
    private static readonly Func<Regex>[] ServingSizeRegexes =
    [
        ServingPortionRegex,
        ServingScoopParenRegex,
        ServingScoopReversedRegex,
        ServingServisRegex,
    ];

    [GeneratedRegex(@"porsiyon[^0-9]{0,25}(?<value>\d+(?:[.,]\d+)?)\s*(?:gr|gram|g)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServingPortionRegex();

    [GeneratedRegex(@"ölçek[^0-9]{0,15}(?<value>\d+(?:[.,]\d+)?)\s*(?:gr|gram|g)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServingScoopParenRegex();

    [GeneratedRegex(@"(?<value>\d+(?:[.,]\d+)?)\s*(?:gr|gram|g)\b[^a-zçğışöü]{0,10}ölçek", RegexOptions.IgnoreCase)]
    private static partial Regex ServingScoopReversedRegex();

    [GeneratedRegex(@"servis[^0-9]{0,20}(?<value>\d+(?:[.,]\d+)?)\s*(?:gr|gram|g)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServingServisRegex();

    [GeneratedRegex(@"(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>gr|g|kg|mg|ml|lt|l|adet|tablet|kaps[uü]l|kaps|caps|softjel|[şs]ase)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex ParenthesesRegex();
}

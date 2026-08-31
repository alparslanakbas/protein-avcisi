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
        ("kilo-hacim", ["gainer", "gain", "mass", "kilo", "hacim", "maltodextrin", "dextrose", "vitargo", "cream of rice", "carbopure", "pirinç unu", "muscle rice"]),
        // Kapsamlı kategori taraması (2026-08-17): vitamin/mineral kategorisinin
        // kendi açıklaması zaten geniş bir yelpaze tanımlıyor ("multivitaminden
        // omega-3'e, magnezyumdan çinkoya") — bu ruhla, önceden hiç bir kategoriye
        // düşmeyen ama gerçek, tanınabilir sağlık takviyesi bileşenleri eklendi.
        // "glucoflex" (Glucosamine+Flex, HIQ'nun eklem sağlığı ürünü, mevcut
        // glucosamine ailesiyle aynı yerde), "curcumin" (zerdeçal özütü) ve
        // "spirulina" (tanınmış bir süperfood takviyesi) de eklendi.
        // Markalı/özel karışım isimleri (GH-UP, Smash Pro, T-Prime vb.) BİLİNÇLİ
        // OLARAK eklenmedi — isimden çıkarım değil tahmin olurdu.
        ("vitamin", ["vitamin", "mineral", "magnesium", "magnezyum", "zinc", "cinko", "omega", "multivitamin", "biotin", "d3k2", "coenzyme", "ginkgo", "glutathione", "hyaluronic", "inulin", "milk thistle", "panax", "ginseng", "psyllium", "rhodiola", "saw palmetto", "selenium", "tribulus", "zma", "glucosamine", "chondroitin", "nmn", "tudca", "ester-c", "5-htp", "b-complex", "lion's mane", "maca", "iron", "chromium", "glucoflex", "curcumin", "spirulina"]),
        ("saglikli-atistirmaliklar", ["bar", "cookie", "kurabiye", "atistirmalik", "rice cake", "pirinc", "fıstık ezmesi", "fıstığı ezmesi", "peanut butter", "ekmek"]),
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

    // Aroma sözlüğü. "Parantez içini ya da tireden sonrasını aroma say" gibi
    // biçimsel bir kural GERÇEK VERİYLE ELENDİ: canlıda dolu olan 80 Flavor
    // değerinin 42'si aroma değildi — porsiyon/miktar ("40 Servis",
    // "15 x 4 Doypacks", "1000 IU"), paket içeriği ("EAA + HellFire
    // Pre-Workout") ve etken madde ("Arginine", "Collagen", "Maca") alana
    // yazılmıştı. Bu alan kullanıcıya "Aroma: 40 Servis" olarak gösteriliyor
    // ve DealsQueryService'te ARAMAYA da dahil, yani yanlış değer hem
    // görünüyor hem eşleşiyordu.
    //
    // Bu yüzden kural tersine çevrildi: aday ancak bilinen bir aroma
    // kelimesiyle eşleşirse kabul ediliyor. Bilinmeyen yeni bir aroma boş
    // kalır — bilinçli takas, "uydurma veri yok" kuralıyla aynı yönde:
    // yanlış göstermektense boş bırak.
    //
    // Liste uydurulmadı, canlı veriden çıkarıldı. İngilizce yazımlar da var
    // çünkü markalar karışık kullanıyor (veride "Creme Caramel" bulundu).
    private static readonly string[] FlavorWords =
    [
        "çikolata", "chocolate", "çilek", "strawberry", "muz", "banana",
        "vanilya", "vanilla", "karamel", "caramel", "kivi", "ananas",
        "pineapple", "limon", "lemon", "portakal", "orange", "mango",
        "ahududu", "raspberry", "karpuz", "elma", "apple", "şeftali",
        "peach", "böğürtlen", "coconut", "fındık", "hazelnut", "bisküvi",
        "biscuit", "kurabiye", "cookie", "kola", "cheesecake", "tiramisu",
        "kakao", "cocoa", "blueberry", "mandalina", "mandarin", "nar",
        "vişne", "cherry", "dondurma", "frambuaz", "kavun", "kiraz",
        "tropik", "tropical", "aromasiz", "naturel", "sade", "bal",
        "tarçın", "fistik", "badem", "latte", "kahve", "coffee", "mocha",
        "nane", "mint", "meyve", "krema", "cream", "yogurt", "yoğurt",
    ];

    // Birden fazla kelimeden oluşanlar ayrı: bunlar token eşleşmesiyle değil
    // doğrudan aranıyor.
    private static readonly string[] FlavorPhrases =
    [
        "hindistan cevizi", "yaban mersini", "orman meyve",
    ];

    /// <summary>
    /// Türkçe ünsüz yumuşaması: ek alan kelimenin son sessizi değişiyor
    /// (çilek → çileği, kitap → kitabı). Sadece "çilek" ile başlayanlara
    /// bakılsaydı "Ereğli Çileği" elenirdi — Yeşilmarka'nın gerçek bir
    /// ürünü. Bu yüzden her aroma kelimesinin yumuşamış gövdesi de
    /// eşleştirmeye giriyor.
    /// </summary>
    private static string SoftenFinalConsonant(string word) => word.Length == 0 ? word : word[^1] switch
    {
        'k' => string.Concat(word.AsSpan(0, word.Length - 1), "ğ"),
        'p' => string.Concat(word.AsSpan(0, word.Length - 1), "b"),
        't' => string.Concat(word.AsSpan(0, word.Length - 1), "d"),
        'ç' => string.Concat(word.AsSpan(0, word.Length - 1), "c"),
        _ => word,
    };

    // Eşleştirmede kullanılan gövdeler: sözlüğün kendisi + yumuşamış hâlleri.
    private static readonly string[] FlavorStems =
        [.. FlavorWords.Concat(FlavorWords.Select(SoftenFinalConsonant)).Distinct()];

    /// <summary>
    /// Türkçe harf tuzağı için normalleştirme. "AROMASIZ" ToLowerInvariant
    /// ile "aromasiz" oluyor ama sözlükteki "aromasız" noktasız ı taşıyor;
    /// "ÇİLEK" ise noktalı İ yüzünden invariant kültürde hiç küçülmüyor.
    /// Noktalı/noktasız ayrımı iki tarafta da siliniyor.
    /// </summary>
    private static string NormalizeForFlavorMatch(string value) =>
        value.Replace('İ', 'i').Replace('I', 'i').Replace('ı', 'i').ToLowerInvariant();

    public static string? ExtractFlavor(string productName)
    {
        foreach (Match match in ParenthesesRegex().Matches(productName))
        {
            var content = match.Groups[1].Value.Trim();
            if (LooksLikeFlavor(content))
                return content;
        }

        // İkinci kaynak: " - Aroma" son eki. Yeşilmarka'nın mağaza API'si her
        // aromayı ayrı ürün olarak döndürüyor ve aromayı ismin sonuna
        // koyuyor ("BCAA 4:1:1 - Ananas"); parantez hiç kullanmıyor.
        var lastDash = productName.LastIndexOf(" - ", StringComparison.Ordinal);
        if (lastDash >= 0)
        {
            var tail = productName[(lastDash + 3)..].Trim();
            if (LooksLikeFlavor(tail))
                return tail;
        }

        return null;
    }

    private static bool LooksLikeFlavor(string candidate)
    {
        if (candidate.Length == 0)
            return false;

        // Rakam taşıyan aday neredeyse her zaman miktar/porsiyon bilgisidir.
        // Canlı veride rakam içeren tek bir gerçek aroma yok.
        if (candidate.Any(char.IsDigit))
            return false;

        // "+" paket içeriği, "®"/"™" marka adı işaretidir.
        if (candidate.Contains('+') || candidate.Contains('®') || candidate.Contains('™'))
            return false;

        if (SizeRegex().IsMatch(candidate))
            return false;

        var normalized = NormalizeForFlavorMatch(candidate);

        if (FlavorPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal)))
            return true;

        // Kelime bazlı eşleşme: "içeriyor mu" yerine "hangi kelimeyle
        // BAŞLIYOR". Böylece Türkçe ekler yakalanıyor ("çilekli", "muzlu",
        // "limonlu") ama kısa kelimeler ("bal", "nar") başka bir kelimenin
        // ortasına denk gelip yanlış eşleşmiyor.
        var tokens = normalized.Split([' ', '-', '/', ',', '.', '&', '(', ')', '*'], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => FlavorStems.Any(stem => token.StartsWith(stem, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Marka adının ürün adı içindeki geçişlerini siler. Marka bilinmiyorsa
    /// ad olduğu gibi döner.
    ///
    /// Not: karşılaştırma OrdinalIgnoreCase — ASCII marka adlarında ("BigJoy",
    /// "Proteinocean") doğru çalışıyor. Türkçe harf içeren marka adlarında
    /// (noktalı/noktasız i) eşleşmeyebilir; o markalarda bu sorun gözlenmedi,
    /// gerekirse normalleştirme eklenir.
    /// </summary>
    private static string StripBrandName(string productName, string? brandName)
    {
        if (string.IsNullOrWhiteSpace(brandName))
            return productName;

        var stripped = productName.Replace(brandName, " ", StringComparison.OrdinalIgnoreCase);

        // Marka adı boşluksuz da yazılabiliyor ("Proteinocean" / "Protein Ocean").
        var compact = brandName.Replace(" ", "", StringComparison.Ordinal);
        if (compact.Length > 3 && compact.Length != brandName.Length)
            stripped = stripped.Replace(compact, " ", StringComparison.OrdinalIgnoreCase);

        // Adın tamamı markadan ibaretse çıkarım yapacak bir şey kalmıyor;
        // orijinali döndürmek yanlış kategoriden iyidir.
        return stripped.Trim().Length == 0 ? productName : stripped;
    }

    /// <summary>
    /// Ürün adından kategori çıkarımı.
    /// </summary>
    /// <param name="brandName">
    /// Biliniyorsa üretici markası; ad içinden ÇIKARILIYOR. Bayi kaynakları
    /// ürün adına markayı da yazıyor ("Proteinocean Creatine 300gr Kreatin
    /// Monohidrat") ve marka adı bir kategori anahtar kelimesi içeriyorsa
    /// ürün yanlış kategoriye düşüyor: gerçek veride ProteinOcean'ın
    /// kreatini, omega'sı ve vitamini "protein tozu" olarak kaydedilmişti,
    /// çünkü "Protein-ocean" içindeki "protein" eşleşiyordu.
    ///
    /// Kategori ürünün NE OLDUĞUNDAN çıkarılmalı, kimin ürettiğinden değil.
    /// </param>
    public static string? InferCategory(string productName, string? brandName = null)
    {
        productName = StripBrandName(productName, brandName);

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

    // Arama kutusu için eşanlamlı gruplar — CategoryKeywords'ten BİLİNÇLİ
    // OLARAK AYRI bir yapı. CategoryKeywords listeleri KATEGORİ TESPİTİ için
    // doğru (bir ürünün hangi kategoriye ait olduğunu belirlemek için geniş/
    // heterojen bir kelime havuzu gerekiyor — "vitamin" kategorisinde 35+
    // birbiriyle alakasız bileşen olması kategori tespiti açısından sorun
    // değil). Ama bu geniş listeleri ARAMA EŞANLAMLISI olarak kullanmak
    // (kullanıcı bir kelime yazınca TÜM kategoriyi eşanlamlı saymak) yanlış
    // sonuç veriyordu — ilk bulgu 2026-08-24: "magnezyum" araması "vitamin"
    // kategorisinin tamamını (NMN, ZMA, Biotin dahil, hiçbiri magnezyumla
    // ilgisi olmayan) eşanlamlı sayıp en üste çıkarıyordu. Kullanıcı sorunca
    // aynı deseni TÜM kategorilerde kontrol ettik — "amino-asitler" (16
    // kelime) ve "kilo-hacim" (10 kelime) de aynı şekilde bozuktu (ör.
    // "taurine"/"glutamin"/"arginin" aramalarının HEPSİ aynı 83 ürünü, aynı
    // sırayla döndürdüğü doğrulandı).
    //
    // Çözüm: her kategori için CategoryKeywords'ü OLDUĞU GİBİ bırakıp (kategori
    // tespiti hiç etkilenmiyor), SADECE gerçekten aynı kavramın farklı yazımı/
    // dili/markası olan DAR alt-grupları burada ayrıca tanımlıyoruz. Aynı
    // kategorideki ama birbirinden farklı bileşenler (ör. "glycine" ve
    // "taurine", ikisi de amino-asitler ama biri diğerinin eşanlamlısı değil)
    // BİLİNÇLİ OLARAK hiçbir grupta yer almıyor — kendi başlarına aranıyorlar.
    private static readonly string[][] SynonymGroups =
    [
        // protein-tozu
        ["isolate", "izole"],
        ["casein", "kazein"],
        ["hipro", "high pro"],
        // kreatin (kategori zaten dar/tutarlı, ama tutarlılık için burada da var)
        ["creatine", "kreatin", "creapure"],
        // amino-asitler — "amino"/"bcaa"/"eaa"/"glycine"/"taurine"/"theanine"/
        // "tyrosine"/"leucine" BİLİNÇLİ OLARAK burada yok, hepsi ayrı amino asit/
        // terim, birbirinin eşanlamlısı değil.
        ["arginin", "arjinin"],
        ["sitrulin", "citrulline"],
        ["alanine", "alanin"],
        ["glutamin", "glutapure"],
        // pre-workout — "pump"/"nitric"/"hellfire"/"caffeine" ayrı kalıyor.
        ["pre workout", "preworkout", "pre-workout"],
        // l-carnitine-cla — "cla" BİLİNÇLİ OLARAK hariç, karnitinden farklı bir
        // bileşen (conjugated linoleic acid), karnitin aramasında çıkmamalı.
        ["l-carnitine", "karnitin", "carnitine", "alcar", "carnifit", "carnıfıt"],
        // yag-yakici (kategori zaten dar/tutarlı)
        ["burner", "yag yakici", "thermo", "termojenik"],
        // kilo-hacim — "mass"/"maltodextrin"/"dextrose"/"vitargo"/"cream of
        // rice"/"carbopure" BİLİNÇLİ OLARAK ayrı, her biri farklı bir
        // karbonhidrat kaynağı/terim.
        ["gainer", "gain", "kilo", "hacim"],
        // vitamin — 2026-08-24'te bulunan ilk vaka.
        ["magnezyum", "magnesium"],
        ["cinko", "çinko", "zinc"],
        // saglikli-atistirmaliklar — "bar"/"atistirmalik" ayrı kalıyor.
        ["cookie", "kurabiye"],
        ["rice cake", "pirinc"],
    ];

    public static IReadOnlyCollection<string> GetSearchSynonyms(string term)
    {
        foreach (var group in SynonymGroups)
        {
            if (group.Contains(term, StringComparer.Ordinal))
                return group;
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

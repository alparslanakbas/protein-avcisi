using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

// Hardline ve ProteinOcean, HIQ'nun aksine ürün isimlerine yapılandırılmış
// bir kategori/etiket bilgisi eklemiyor (Hardline hiç kategori vermiyor,
// ProteinOcean'ın GraphQL API'si tek "Tüm Ürünler" kategorisi kullanıyor) —
// bu yüzden HIQ'daki gibi Shopify "tags" ("type:wearable"/"type:equipment")
// bazlı bir filtre burada mümkün değil, isim bazlı bir kelime listesi
// kullanılıyor. Site kapsamı spor takviyesi/protein — tişört, hoodie,
// şapka, anahtarlık, huni, pillbox gibi giyim/aksesuar ürünleri kullanıcı
// isteğiyle kapsam dışı bırakıldı (bkz. CLAUDE.md, 2026-08-23).
public static partial class NonSupplementProductFilter
{
    public static bool IsAccessoryOrApparel(string productName)
    {
        // TÜRKÇE HARFLER ASCII'YE İNDİRİLİYOR — kalıplar çalışmadan ÖNCE.
        //
        // .NET'in RegexOptions.IgnoreCase'i invariant kültürle çalışıyor ve
        // Türkçe harf çiftlerini katlamıyor. Sonuç: "EFFİVE BLACK PİLLBOX"
        // adlı ürün `pillbox` kalıbına TAKILMIYORDU ve canlı katalogda
        // aksesuar olarak duruyordu (3 Eylül'de 2991 ürün taranırken bulundu).
        // Aynı tuzak noktasız I için de var — "ANAHTARLIK" gibi tamamı büyük
        // yazılmış adlar `anahtarlık` kalıbıyla eşleşmiyordu; Grizzone'un
        // kataloğu baştan sona büyük harf.
        //
        // Kalıplar da bu yüzden SAF ASCII yazıldı. Öncesinde her Türkçe
        // kelimenin ASCII kopyası ayrıca tutuluyordu ("esofman", "agirlik
        // kemer", "bakim seti", "ketcap"...); tek biçime inince o çoğaltma
        // gereksizleşti ve eksik kalan yazımlar da kapandı.
        productName = TurkceyiAsciiyeIndir(productName);

        var match = AccessoryKeywordRegex().Match(productName);
        if (!match.Success)
            return false;

        // HEDİYE SHAKER İSTİSNASI. Markalar takviye paketlerinin yanında
        // shaker veriyor ve adına yazıyor: "Kilo Aldırıcı Ultra Set - Shaker
        // Hediyeli" (7.200 TL), "HIQ Fitness Başlangıç Paketi + Shaker".
        // Bunlar aksesuar değil, aksesuar HEDİYELİ takviye — elenirlerse
        // gerçek ürün kaybediyoruz.
        //
        // Katalog tarandı (2 Eylül, 1456 ad): "shaker" geçen ÜÇ ürünün üçü de
        // bu tipte, gerçek shaker hiç yok (onlar zaten yutulurken eleniyor).
        // Gerçek shaker'larda da bu işaretler hiç geçmiyor — o gün görülenler:
        // "Renkli Yüksek Kalite Shaker 550cc", "Space Shaker",
        // "Prime Nutrition Shaker 500 ml.".
        //
        // İstisna DAR: yalnızca eşleşen tek kelime "shaker" olduğunda ve adda
        // hediye/ekleme işareti varken geçerli. "Spor Çantası Hediyeli" gibi
        // bir ad hâlâ eleniyor, çünkü orada eşleşen kelime "çanta".

        // TAKVİYE PAKETİ İSTİSNASI. Adında hem PAKET/SET işareti hem de bir
        // takviye bileşeni geçen ürünler, içindeki aksesuarlar yüzünden
        // elenmemeli — o aksesuarlar ürünün KENDİSİ değil, paketin içeriği.
        //
        // 3 Eylül'de Grizzone kataloğunda ölçülerek bulundu; süzgeç üç GERÇEK
        // takviye paketini eliyordu:
        //   "FİTNESS PAKETİ - PROFESYONEL (WHEY PROTEİN PRO 1800 GR + ...)"
        //      -> shaker, havlu, strap, anahtarlık kelimelerine takılıyordu
        //   "GRIZZY IRON PACK (WHEY PROTEIN PRO + ZINC+D3+C + GRIZZONE SHAKER)"
        //   "FİTNESS PAKETİ - ORTA (WHEY PROTEIN 420 GR + BCAA 500 GR + ...)"
        //
        // Aşağıdaki "hediye shaker" istisnası bunları KURTARMIYORDU: o istisna
        // yalnızca TEK eşleşme varken ve "+shaker" bitişikken geçerli.
        //
        // İki şart birlikte aranıyor, çünkü tek başına ikisi de yetersiz:
        // "Grizzone SACKPACK Spor Çanta" içinde "pack" geçiyor (kelime sınırı
        // bunu eler) ve "Gıda paketi (PANCAKE + ... SOS)" paket işareti taşıyor
        // ama takviye bileşeni taşımıyor — ikisi de doğru şekilde eleniyor.
        //
        // İstisna GİYSİ ve ÇANTAYA UZANMIYOR. Mevcut kural şunu diyor:
        // "hediyeli de olsa çanta çantadır" (bkz. HediyeliCantaYineDeEleniyor
        // testi). Grizzone'da kurtarılan paketlerin içindekiler shaker, havlu,
        // strap ve anahtarlık — yani tipik hediye kalemleri; giysi/çanta
        // geçen bir adda paket istisnası çalışmıyor, ürün yine eleniyor.
        if (BundleMarkerRegex().IsMatch(productName)
            && SupplementMarkerRegex().IsMatch(productName)
            && !ApparelOrBagRegex().IsMatch(productName))
        {
            return false;
        }

        var yalnizcaShaker = match.Value.StartsWith("shaker", StringComparison.OrdinalIgnoreCase)
            && AccessoryKeywordRegex().Matches(productName).Count == 1;

        return !(yalnizcaShaker && GiftedAccessoryRegex().IsMatch(productName));
    }

    // Not: liste, kaçan ürünler bulundukça genişliyor — korse/eşofman/çanta
    // 28 Ağustos'ta eklendi (ilk temizlik turunda gözden kaçmışlardı).
    //
    // 31 Ağustos'ta gıda/çeşni grubu eklendi (basmati, himalaya tuzu, hardal,
    // sriracha, sweet drops): Commander Nutrition katalogunda pirinç, tuz, sos
    // ve sıvı tatlandırıcı da satılıyor — bunlar spor takviyesi değil.
    //
    // "pirinç"/"rice" BİLİNÇLİ OLARAK LİSTEDE YOK: "Cream of Rice" gerçek bir
    // sporcu gıdası ve aynı katalogda mevcut ("Dr. Pan Rice Cream"). Genel
    // kelime yerine yalnızca ayırt edici olanlar (basmati gibi) kullanılıyor —
    // "performans"ın dışarıda bırakılmasıyla aynı gerekçe.
    // "performans" gibi genel kelimeler BİLİNÇLİ olarak yok: markaların gerçek
    // takviye paketleri de o kelimeyi taşıyor (ör. "orta-guc-performans").
    //
    // 1 Eylül'de İKİ TUR düzeltme yapıldı; ikisi de canlıya ürün girdikten
    // SONRA fark edildi, yani liste hâlâ "kaçan ürün buldukça genişliyor".
    // Yeni bir bayi kataloğu eklendiğinde bu sorgu çalıştırılmalı:
    //   SELECT "Name" FROM "Products" WHERE lower("Name") ~ '(atlet|havlu|çanta|strap|box|kemer|...)';
    // Sonucu gözle elemek şart — "Kutu" meşru çoklu paketlerde de geçiyor
    // ("Protein Bar 16lı Kutu"), o yüzden kör silme yapılmamalı.
    //
    // İkinci tur (aynı gün): "Atleti" kaçtı çünkü ek desteği "havlu"/"çanta"ya
    // eklenip "atlet"e eklenmemişti; "8 Loop Strap" kaçtı çünkü kalıp yalnızca
    // "lifting strap" idi; "Pill Box"/"Powder Box" kaçtı çünkü listede
    // yalnızca bitişik "pillbox" vardı. Ders: kalıbı ürünün TÜRÜNE göre yaz,
    // gördüğün tek yazıma göre değil.
    //
    // "atlet" bilinçli olarak `[a-zçğıöşü]*` ile YAZILMADI: o hâli "atletik"
    // kelimesini de yakalar ve "atletik performans" meşru bir takviye ifadesi.
    //
    // 1 Eylül'de TÜRKÇE EK sorunu düzeltildi. Listede "havlu" ve "çanta"
    // vardı ama Provitamin kataloğundan "Antrenman HAVLUSU" ve "Spor
    // ÇANTASI" geçti: `\b` kelime sınırı ekten ÖNCE kırılmıyor, yani
    // "havlu" kalıbı "havlusu" ile eşleşmiyor. Ek alabilen isimlere
    // `[a-zçğıöşü]*` eklendi — kodda bu numara zaten kullanılıyordu
    // (`eldiven[a-zçğıöşü]*`), sadece tutarsız uygulanmıştı.
    // "canta[a-z]*" DEĞİL: o hâli "Cantaloupe"u yakalıyordu ve katalogda
    // gerçek bir kurban var — "Nois Whey Rex 900G Protein Tozu - Cantaloupe".
    // Bir whey proteini spor çantası sanıp sessizce eleyecekti. Nois scraper'ı
    // bu yüzden ortak süzgeci hiç kullanmıyor, kaynağın kendi "Aksesuar"
    // kategorisine bakıyor. Kalıp artık AÇIK EK LİSTESİ kullanıyor —
    // "atlet(i|ler|leri)?" için daha önce verilen kararın aynısı: Türkçe ek
    // serbest bırakılınca başka kelimelerin içine denk geliyor.
    // 1416 ürünlük katalogla doğrulandı: yalnızca gerçek aksesuarlar kalıyor.
    //
    // 3 Eylül'de iki yeni kaynağın kataloğu ÖLÇÜLEREK genişletildi (tahminle
    // değil: 79 + 86 ürünün tamamı çekilip mevcut kalıp üzerinden geçirildi,
    // neyin kaçtığı listelendi).
    //   Gigi's: 8 el yapımı seramik KASE / kuru yemişlik kaçıyordu (çantaları
    //   kalıp zaten yakalıyordu).
    //   MLA Protein: BBQ Sos, Şekersiz Ketçap, Garlic powder, Hot Chili,
    //   Cajun/Chicken/Vegetable/BBQ Mix, Sprey Yağ, Aromalı Tatlandırıcı
    //   kaçıyordu (hardal/sriracha/himalaya tuzu/shaker zaten yakalanıyordu).
    //
    // "sos" kelime sınırıyla ve açık ek listesiyle yazıldı: "sos(u|lar|ları)?"
    // — bu hâli "SOSis"i YAKALAMAZ, çünkü "sos"tan sonra gelen "is" listede
    // yok ve sınır tutmuyor. Aynı gerekçe "kase" için de geçerli.
    //
    // "Flavor Chocolate" BİLİNÇLİ OLARAK eklenmedi: tek bir üründe geçiyor ve
    // "flavor/chocolate" gibi genel kelimeler gerçek aromalı ürünleri elerdi.
    // Bir çeşni ürününü kaçırmak, bir protein tozunu elemekten iyidir.
    [GeneratedRegex(
        @"\b(t-?shirt|sweatshirt|hoodie|sapka[a-z]*|beyzbol|pillbox|pill ?box|powder ?box|saklama kabi|bileklik[a-z]*|havlu[a-z]*|buff|atlet(i|ler|leri)?|anahtarlik[a-z]*|maskot|huni[a-z]*|shaker[a-z]*|sort[a-z]*|korse[a-z]*|esofman[a-z]*|canta(si|lar|lari)?|handbag|direnc band[a-z]*|loop band[a-z]*|strap[a-z]*|wrist wrap[a-z]*|agirlik kemer[a-z]*|dip belt[a-z]*|eldiven[a-z]*|hap kutusu|bakim seti|seyahat seti|kase(si|ler|leri)?|kuru yemislik|basmati|himalaya tuzu|hardal|sriracha|sweet drops|sos(u|lar|lari)?|ketcap|ketchup|garlic powder|hot chili|cajun|chicken mix|vegetable mix|bbq|sprey yag[i]?|tatlandirici)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex AccessoryKeywordRegex();

    /// <summary>
    /// "Hediyeli" / "+ Shaker" — aksesuarın ürünün KENDİSİ değil, yanında
    /// verilen bir ek olduğunu söyleyen işaretler. Türkçe noktalı İ tuzağı
    /// yok (hepsi ASCII harf), IgnoreCase yeterli.
    /// </summary>
    [GeneratedRegex(@"(hediye[a-z]*|\+\s*shaker)", RegexOptions.IgnoreCase)]
    private static partial Regex GiftedAccessoryRegex();

    /// <summary>
    /// Paket/set işareti. KELİME SINIRLI: "Sackpack" gibi adlar "pack"
    /// içerdiği için sınırsız kalıp gerçek bir çantayı kurtarırdı.
    /// </summary>
    [GeneratedRegex(@"\b(paket|paketi|paketleri|set|seti|setleri|pack|kit)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BundleMarkerRegex();

    /// <summary>
    /// Giysi ve çanta grubu — paket istisnasının UZANMADIĞI aksesuarlar.
    /// Mevcut kural: "hediyeli de olsa çanta çantadır".
    /// Kalıp ASCII, çünkü ad zaten ASCII'ye indirgenmiş olarak geliyor.
    /// </summary>
    [GeneratedRegex(@"\b(t-?shirt|sweatshirt|hoodie|sapka[a-z]*|sort[a-z]*|korse[a-z]*|esofman[a-z]*|canta(si|lar|lari)?|handbag|atlet(i|ler|leri)?|maskot)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ApparelOrBagRegex();

    /// <summary>
    /// Adın içinde gerçek bir takviye bileşeni geçiyor mu? Paket istisnası
    /// yalnızca bununla birlikte çalışıyor — yoksa "Gıda paketi (PANCAKE +
    /// ÇİKOLATA SOS)" gibi çeşni paketleri de kurtulurdu.
    /// </summary>
    [GeneratedRegex(@"\b(whey|protein|proteini|bcaa|eaa|kreatin|creatine|amino|vitamin|gainer|glutamin|glutamine|kolajen|collagen|karnitin|carnitine|arginin|arginine)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SupplementMarkerRegex();

    /// <summary>
    /// Türkçe harfleri ASCII karşılığına indirger. Kalıplar bu alfabede
    /// yazıldığı için büyük/küçük harf tuzakları tek noktada bitiyor.
    /// </summary>
    private static string TurkceyiAsciiyeIndir(string value)
    {
        Span<char> tampon = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            tampon[i] = value[i] switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' or 'I' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                var c => c,
            };
        }

        return new string(tampon);
    }
}

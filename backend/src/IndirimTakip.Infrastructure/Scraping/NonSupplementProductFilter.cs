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
    public static bool IsAccessoryOrApparel(string productName) =>
        AccessoryKeywordRegex().IsMatch(productName);

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
    // 1 Eylül'de TÜRKÇE EK sorunu düzeltildi. Listede "havlu" ve "çanta"
    // vardı ama Provitamin kataloğundan "Antrenman HAVLUSU" ve "Spor
    // ÇANTASI" geçti: `` kelime sınırı ekten ÖNCE kırılmıyor, yani
    // "havlu" kalıbı "havlusu" ile eşleşmiyor. Ek alabilen isimlere
    // `[a-zçğıöşü]*` eklendi — kodda bu numara zaten kullanılıyordu
    // (`eldiven[a-zçğıöşü]*`), sadece tutarsız uygulanmıştı.
    [GeneratedRegex(
        @"\b(t-?shirt|sweatshirt|hoodie|şapka[a-zçğıöşü]*|beyzbol|pillbox|bileklik[a-zçğıöşü]*|havlu[a-zçğıöşü]*|buff|atlet|anahtarlık[a-zçğıöşü]*|maskot|huni[a-zçğıöşü]*|shaker[a-zçğıöşü]*|şort[a-zçğıöşü]*|korse[a-zçğıöşü]*|eşofman[a-zçğıöşü]*|esofman[a-z]*|çanta[a-zçğıöşü]*|canta[a-z]*|handbag|direnç band[a-zçğıöşü]*|direnc band[a-z]*|loop band[a-z]*|lifting strap[a-z]*|wrist wrap[a-z]*|ağırlık kemer[a-zçğıöşü]*|agirlik kemer[a-z]*|dip belt[a-z]*|eldiven[a-zçğıöşü]*|hap kutusu|bakım seti|bakim seti|seyahat seti|basmati|himalaya tuzu|hardal|sriracha|sweet drops)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex AccessoryKeywordRegex();
}

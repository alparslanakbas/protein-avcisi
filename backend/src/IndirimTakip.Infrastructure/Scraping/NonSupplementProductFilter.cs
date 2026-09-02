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
    // ÇANTASI" geçti: `` kelime sınırı ekten ÖNCE kırılmıyor, yani
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
    [GeneratedRegex(
        @"\b(t-?shirt|sweatshirt|hoodie|şapka[a-zçğıöşü]*|beyzbol|pillbox|pill ?box|powder ?box|saklama kab[ıi]|bileklik[a-zçğıöşü]*|havlu[a-zçğıöşü]*|buff|atlet(i|ler|leri)?|anahtarlık[a-zçğıöşü]*|maskot|huni[a-zçğıöşü]*|shaker[a-zçğıöşü]*|şort[a-zçğıöşü]*|korse[a-zçğıöşü]*|eşofman[a-zçğıöşü]*|esofman[a-z]*|çanta[a-zçğıöşü]*|canta(s[ıi]|lar|lar[ıi])?|handbag|direnç band[a-zçğıöşü]*|direnc band[a-z]*|loop band[a-z]*|strap[a-z]*|wrist wrap[a-z]*|ağırlık kemer[a-zçğıöşü]*|agirlik kemer[a-z]*|dip belt[a-z]*|eldiven[a-zçğıöşü]*|hap kutusu|bakım seti|bakim seti|seyahat seti|basmati|himalaya tuzu|hardal|sriracha|sweet drops)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex AccessoryKeywordRegex();
}

// Arama kutusundaki metni karşılaştırmaya hazırlar.
//
// NEDEN AYRI BİR DOSYA: markalar dizinindeki arama, metni
// `toLocaleLowerCase('tr-TR')` ile küçültüyordu ve bu, büyük ASCII "I"yı
// NOKTASIZ "ı"ya çeviriyor: "HIQ" -> "hıq", "Imperium" -> "ımperium".
// Kullanıcı doğal olarak küçük harfle "hiq" yazdığında arama 0 sonuç
// veriyordu; ürün yalnızca "HIQ" diye büyük yazılınca bulunuyordu.
// Bu, depoda daha önce iki kez yaşanan hatanın aynısı (bkz. `c59acb3`:
// backend'de `.ToLower()` invariant kültürde "İ"yi küçültmüyordu ve
// "VİTAMİN" araması 0 sonuç veriyordu).
//
// YAKLAŞIM `slugify.ts` ile AYNI: locale'e bağlı küçültmeye hiç güvenilmiyor,
// Türkçe'ye özgü harfler ELLE eşleniyor, sonra kalan düz ASCII için
// culture-bağımsız `toLowerCase()` kullanılıyor.
//
// slugify DOĞRUDAN KULLANILAMADI: o fonksiyon slug'ı 80 karakterde kırpıyor
// (URL için doğru), oysa buradaki aranabilir metin marka adı + tüm kategori
// etiketlerinden oluşuyor ve rahatlıkla 80 karakteri aşıyor — kırpılsaydı
// sondaki kategoriler aranamaz hâle gelirdi.
const TURKCE_HARF_ESLEMESI: Record<string, string> = {
  ç: 'c',
  Ç: 'c',
  ğ: 'g',
  Ğ: 'g',
  ı: 'i',
  İ: 'i',
  ö: 'o',
  Ö: 'o',
  ş: 's',
  Ş: 's',
  ü: 'u',
  Ü: 'u',
};

/**
 * Türkçe harfleri ASCII karşılıklarına indirger, küçültür ve ardışık
 * boşlukları teke düşürür.
 *
 * Aynı dönüşüm hem aranan metne hem aranacak metne uygulandığı için iki
 * taraf her yazımda buluşuyor: "hiq" / "HIQ" / "Hiq" aynı sonucu verir,
 * "fit carsi" yazarak "Fit Çarşı" bulunur.
 */
export function normalizeSearchText(value: string): string {
  return value
    .replace(/[çÇğĞıİöÖşŞüÜ]/g, (ch) => TURKCE_HARF_ESLEMESI[ch] ?? ch)
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}

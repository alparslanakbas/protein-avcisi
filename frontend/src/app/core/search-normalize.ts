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

/**
 * Aranan metin, hedef metinle eşleşiyor mu?
 *
 * <b>NEDEN DÜZ `includes` YETMİYOR.</b> Marka adları boşluk konusunda tutarsız
 * yazılıyor: "ProteinOcean" bitişik, "Swiss Nutrition" ayrık. Kullanıcı
 * "protein ocean" yazdığında hiçbir sonuç çıkmıyordu, çünkü "proteinocean"
 * içinde "protein ocean" geçmiyor. Kullanıcı bunu bildirdi: "diğer markaları
 * buluyor ama ProteinOcean'ı bulmuyor".
 *
 * İki yönlü çözüm — biri tutarsa eşleşiyor:
 * 1. <b>Kelime kelime:</b> aranan metnin HER kelimesi hedefte geçiyorsa.
 *    "protein ocean" -> "protein" ✓ ve "ocean" ✓ -> ProteinOcean bulunur.
 *    Sıra da önemsizleşir: "nutrition swiss" -> Swiss Nutrition.
 * 2. <b>Boşluksuz:</b> iki taraftan da boşluklar atılıp karşılaştırılır.
 *    "swissnutrition" -> "swissnutrition" ✓ -> Swiss Nutrition bulunur.
 *
 * Ürün aramasındaki (backend) mantıkla aynı ruhta: kelimeler arası AND.
 */
export function matchesSearch(searchable: string, query: string): boolean {
  const hedef = normalizeSearchText(searchable);
  const aranan = normalizeSearchText(query);
  if (!aranan) return true;

  const kelimeler = aranan.split(' ').filter(Boolean);
  if (kelimeler.every((kelime) => hedef.includes(kelime))) return true;

  return hedef.replace(/ /g, '').includes(aranan.replace(/ /g, ''));
}

// Scraper'dan gelen ham ürün isimleri markaya göre değişiyor — bazıları
// tamamen BÜYÜK HARF ("23.YIL AVANTAJ PAKETİ 1", "NOX2 540 GR"), bazıları
// zaten normal Title Case. ALL CAPS bir başlık/H1/SERP snippet'i hem
// okunabilirliği düşürüyor hem spam sinyali gibi duruyor. Bu fonksiyon
// SADECE görüntüleme metinlerinde (title, description, H1, kart başlığı,
// JSON-LD name) kullanılıyor — URL slug'ı hâlâ orijinal productName'den
// (slugify ile) üretiliyor, burası hiç etkilemiyor.
//
// tr-TR locale'i bilinçli kullanılıyor (toLocaleUpperCase/LowerCase) —
// düz .toUpperCase()/.toLowerCase() İngilizce kurallarla çalışıp "İ"/"I"
// harflerini yanlış eşler (backend'de ProductAttributeParser'da daha önce
// yaşanan aynı sınıf hata, bkz. CLAUDE.md "tr-TR ToLower bug'ı"). AMA ürün
// isimleri Türkçe VE İngilizce kelimeleri karışık taşıyor ("Tanışma Paketi"
// yanında "Creatine", "Isolate", "Whey" gibi İngilizce teknik terimler) —
// tr-TR kuralı İngilizce bir kelimedeki büyük "I" harfini "ı"ya çevirip
// "CREATINE"yi "creatıne" yapardı (yanlış). Bunun için bilinen İngilizce
// teknik terimler ayrı bir listede tutulup onlar için düz (locale
// bağımsız) küçültme/büyütme kullanılıyor — geri kalan (varsayılan Türkçe)
// kelimeler tr-TR kuralıyla işleniyor.

const ACRONYMS = new Set([
  'HIQ', 'SSN', 'BCAA', 'EAA', 'WPC', 'WPI', 'WPH', 'CLA', 'ZMA', 'GI',
  'NOX', 'PRE', 'ISO', 'GR', 'ML', 'KG', 'L', 'PLUS',
]);

const LOWERCASE_WORDS = new Set(['ve', 'ile', 'ya', 'da', 'de']);

// Sık geçen İngilizce takviye terimleri — "I" harfi tr-TR kuralıyla yanlış
// (ı yerine i olması gerekirken) küçülen kelimeler. Liste tam kapsayıcı
// olmak zorunda değil (amaç mükemmel dilbilgisi değil, ALL CAPS'tan
// kurtulmak) — yeni bir kelime bu listede yoksa tr-TR'ye düşer, en kötü
// ihtimalle "Creatıne" gibi kozmetik bir sapma olur, anlam bozulmaz.
const ENGLISH_TERMS = new Set([
  'CREATINE', 'ISOLATE', 'WHEY', 'FUSION', 'GAINER', 'MATRIX', 'COMPLEX',
  'MICRONIZED', 'HYDROLYZED', 'CONCENTRATE', 'GLUTAMINE', 'ARGININE',
  'CITRULLINE', 'TAURINE', 'CARNITINE', 'CAFFEINE', 'NIACIN', 'MIX',
  'CROSSFIT', 'FIT', 'FITNESS',
]);

function formatWord(word: string): string {
  const upper = word.toLocaleUpperCase('tr-TR');

  // Zaten tamamen büyük harfli değilse (küçük veya karışık case) dokunma —
  // hem idempotent kalır hem "gr"/"ml" gibi zaten okunabilir yazımları
  // gereksiz yere değiştirmez.
  if (word !== upper) return word;

  const isEnglish = ENGLISH_TERMS.has(upper);
  const lower = isEnglish ? word.toLowerCase() : word.toLocaleLowerCase('tr-TR');

  if (LOWERCASE_WORDS.has(lower)) return lower;
  if (ACRONYMS.has(upper)) return upper;

  // Kısa (1-2 harf), tamamen büyük, bilinmeyen bir token — muhtemelen
  // listede olmayan bir birim/kısaltma (ör. "XL"). Tahmin etmek yerine
  // olduğu gibi bırakıyoruz.
  if (word.length <= 2) return word;

  const firstUpper = isEnglish ? lower.charAt(0).toUpperCase() : lower.charAt(0).toLocaleUpperCase('tr-TR');
  return firstUpper + lower.slice(1);
}

export function displayName(raw: string): string {
  if (!raw) return raw;
  // \p{L}+ — yalnızca harf gruplarını yakalayıp işliyoruz; sayılar,
  // parantezler, tireler ve diğer noktalama olduğu gibi kalıyor
  // (ör. "240g", "(36 GR*15 ADET)", "%100" hiç bozulmuyor).
  return raw.replace(/\p{L}+/gu, formatWord);
}

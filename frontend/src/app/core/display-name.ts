// Scraper'dan gelen ham ürün isimleri markaya göre değişiyor — bazıları
// tamamen BÜYÜK HARF ("23.YIL AVANTAJ PAKETİ 1", "NOX2 540 GR"), bazıları
// zaten normal Title Case. ALL CAPS bir başlık/H1/SERP snippet'i hem
// okunabilirliği düşürüyor hem spam sinyali gibi duruyor. Bu fonksiyon
// SADECE görüntüleme metinlerinde (title, description, H1, kart başlığı,
// JSON-LD name) kullanılıyor — URL slug'ı hâlâ orijinal productName'den
// (slugify ile) üretiliyor, burası hiç etkilemiyor.
//
// HANGİ KELİMEYE HANGİ KÜLTÜR KURALI UYGULANIR
//
// Türkçe'de büyük "I" küçülünce noktasız "ı" olur; İngilizce'de "i". Ürün
// isimleri iki dili karışık taşıdığı için tek bir kural her ikisini birden
// doğru veremiyor. Sorun, hangisinin VARSAYILAN olacağı.
//
// Önce tr-TR varsayılandı ve İngilizce terimler bir istisna listesinde
// tutuluyordu. Katalog büyüdükçe liste geride kaldı: canlıdaki 1748 sayfa
// ölçüldüğünde ~200 sayfada "Proteın", "Bıgwhey", "Hyaluronıc Acıd",
// "Magnesıum", "Vıtamın" gibi bozuk yazımlar çıktı. Bunlar kozmetik
// değildi — "protein bar" arayan birinin sorgusu "Proteın" ile
// eşleşmiyor ve aynı metin schema.org `name` alanına da gidiyor.
//
// Bu yüzden varsayılan TERS ÇEVRİLDİ: kelimede Türkçe'ye özgü bir harf
// (ç ğ ö ş ü İ) yoksa İngilizce varsayılıp kültürden bağımsız küçültme
// yapılıyor. Ölçüm bunun neden doğru taraf olduğunu gösteriyor —
// katalogdaki 98 riskli kelimenin 90'ı İngilizce, yalnızca 8'i Türkçe.
// İngilizce terim çeşitliliği her yeni ürünle büyüyor; Türkçe'de "I"
// içeren kelime havuzu ise küçük ve sabit, o yüzden istisna listesini
// taşıması gereken taraf o.
//
// Yan fayda: markaların "EKONOMIK", "MIKRONIZE", "KREATIN" gibi noktalı
// İ yerine I yazdığı kelimeler de artık doğru çıkıyor ("Ekonomik"),
// eskiden "Ekonomık" oluyordu.

const ACRONYMS = new Set([
  'HIQ', 'SSN', 'BCAA', 'EAA', 'WPC', 'WPI', 'WPH', 'CLA', 'ZMA', 'GI',
  'NOX', 'PRE', 'ISO', 'GR', 'ML', 'KG', 'L', 'PLUS',
  // GNC eklendikten sonra canlıda 45 ürünün H1'i tarandı (2 Eylül): marka
  // adının kendisi "Gnc" çıkıyordu (22 sayfa), ayrıca "5 - HTP" → "Htp",
  // "AMP - Wheybolic" → "Amp", "400 MCG" → "Mcg". MCG diğer birimlerle
  // (GR/ML/KG) aynı mantıkta: kaynaktaki yazım korunuyor, yani "mcg" küçük
  // yazılmışsa küçük kalmaya devam ediyor.
  'GNC', 'HTP', 'AMP', 'MCG',
]);

// Başlık içinde küçük kalması gereken bağlaçlar. İngilizce olanlar
// katalogda gerçekten geçtiği için eklendi ("CREAM OF RICE" → "Cream of
// Rice"); "in", "on", "a" bilinçli olarak DIŞARIDA — "VITAMIN A"yı
// "Vitamin a" yapar, "ON" ise bir marka kısaltması olabilir.
//
// Bu liste kelimenin konumuna bakmıyor: ürün adı bir bağlaçla başlarsa
// küçük harfle başlar. Katalogda böyle bir ad yok (ölçüldü) ve Türkçe
// bağlaçlarda da aynı davranış uzun süredir sorunsuz.
const LOWERCASE_WORDS = new Set([
  've', 'ile', 'ya', 'da', 'de',
  'of', 'the', 'and', 'for', 'with',
]);

// Türkçe'ye özgü harf TAŞIMAYAN ama yine de Türkçe olan kelimeler —
// tek ayırt edici işaretleri sondaki/içteki noktasız "ı". Bu liste
// katalogdaki 1005 ürün adı taranarak çıkarıldı, tahminle değil: ALL-CAPS
// gelen, "I" içeren ve Türkçe harf taşımayan 98 kelimeden Türkçe olanlar
// bunlar. Yeni bir marka eklendiğinde listeye eklenmesi gerekebilir; eksik
// kalırsa hata sınırlı ve görünür olur ("Aromali" gibi), sessizce yanlış
// bir dile kaymaz.
const TURKISH_ONLY_WORDS = new Set([
  'YAPILANMASI', 'YIL', 'BAHARATI', 'FISTIK', 'AROMALI', 'FINDIK',
  'SARIMSAK', 'KREMASI',
]);

// Türkçe'ye özgü harfler. Biri bile geçiyorsa kelime Türkçe'dir ve
// tr-TR kuralı uygulanır. ("ı" büyük halde "I" olduğu için burada yok —
// zaten ALL-CAPS kelimelerde görünmez.)
const TURKISH_LETTERS = /[ÇĞİÖŞÜ]/;

function formatWord(word: string): string {
  const upper = word.toLocaleUpperCase('tr-TR');

  // Zaten tamamen büyük harfli değilse (küçük veya karışık case) dokunma —
  // hem idempotent kalır hem "gr"/"ml" gibi zaten okunabilir yazımları
  // gereksiz yere değiştirmez.
  if (word !== upper) return word;

  const isTurkish = TURKISH_LETTERS.test(upper) || TURKISH_ONLY_WORDS.has(upper);
  const lower = isTurkish ? word.toLocaleLowerCase('tr-TR') : word.toLowerCase();

  if (LOWERCASE_WORDS.has(lower)) return lower;
  if (ACRONYMS.has(upper)) return upper;

  // Kısa (1-2 harf), tamamen büyük, bilinmeyen bir token — muhtemelen
  // listede olmayan bir birim/kısaltma (ör. "XL"). Tahmin etmek yerine
  // olduğu gibi bırakıyoruz.
  if (word.length <= 2) return word;

  const firstUpper = isTurkish ? lower.charAt(0).toLocaleUpperCase('tr-TR') : lower.charAt(0).toUpperCase();
  return firstUpper + lower.slice(1);
}

export function displayName(raw: string): string {
  if (!raw) return raw;
  // \p{L}+ — yalnızca harf gruplarını yakalayıp işliyoruz; sayılar,
  // parantezler, tireler ve diğer noktalama olduğu gibi kalıyor
  // (ör. "240g", "(36 GR*15 ADET)", "%100" hiç bozulmuyor).
  return raw.replace(/\p{L}+/gu, formatWord);
}

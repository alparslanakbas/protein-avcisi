// Ürün adından URL-güvenli bir slug üretir (/urun/:id/:slug için, SEO amaçlı
// — CTR ve URL'de anahtar kelime sinyali). Türkçe karakterler backend'in
// ProductAttributeParser'ında yaşanan aynı dersle (CultureInfo'lu
// ToLower()'ın "İ" harfinde beklenmedik davranışı) tutarlı olsun diye,
// genel/locale'e bağlı bir küçültmeye güvenmeden ELLE eşleniyor — sonrasında
// kullanılan toLowerCase() sadece düz ASCII harfleri işliyor, culture-bağımsız.
const TURKISH_CHAR_MAP: Record<string, string> = {
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

// Bazı bundle/kombinasyon ürün isimleri çok uzun ("SSN Whey Refuel 1800g
// Çikolatalı + SSN Creatine 300g + ..." gibi) — aşırı uzun bir URL segmenti
// istemiyoruz, kelime ortasından kesmemek için son tire sınırına kadar kırpılıyor.
const MAX_SLUG_LENGTH = 80;

export function slugify(text: string): string {
  const mapped = text.replace(/[çÇğĞıİöÖşŞüÜ]/g, (ch) => TURKISH_CHAR_MAP[ch] ?? ch);
  const normalized = mapped
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');

  if (normalized.length <= MAX_SLUG_LENGTH) return normalized;

  const truncated = normalized.slice(0, MAX_SLUG_LENGTH);
  const lastHyphen = truncated.lastIndexOf('-');
  return lastHyphen > 0 ? truncated.slice(0, lastHyphen) : truncated;
}

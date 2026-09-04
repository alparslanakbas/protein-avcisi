import { brandSlug } from './brand-slug';

/**
 * Logosu indirilip `frontend/public/marka-logo/` altına konmuş markalar.
 *
 * <b>NEDEN KENDİ SUNUCUMUZDAN:</b> logolar önce markaların KENDİ CDN'lerinden
 * hotlink'leniyordu. Bunun iki sorunu var:
 * 1. Erişilemeyen bir host `onerror` TETİKLEMİYOR — istek asılı kalıyor ve
 *    yedek ikon hiç devreye girmiyor, kart süresiz boş duruyor. 3 Eylül
 *    gecesi Muscle Pump'ın sunucusu tam bunu yaptı (o gün 502 de verdi).
 * 2. Marka logosunu değiştirdiğinde/taşıdığında kart sessizce boşalıyor.
 *
 * Dosyalar 128px WebP'ye indirildi: 584 kB -> 54 kB.
 *
 * <b>LİSTE ELLE TUTULUYOR, ÜRETİLMİYOR:</b> yalnızca adresi GERÇEKTEN
 * kendisine ait olan markaların logosu var. Katalogdaki 89 markanın 66'sı
 * yalnızca bir bayiden geliyor ve veritabanındaki `BaseUrl`'leri bayinin
 * adresi (Olimp -> protein7.com); oradan favicon çekmek onlarca markaya
 * protein7'nin logosunu koyardı. O markalarda logo UYDURULMUYOR, monogram
 * gösteriliyor.
 */
const YEREL_LOGOLU_MARKALAR: ReadonlySet<string> = new Set([
  'bahs',
  'bigjoy',
  'biofit',
  'commander-nutrition',
  'dr-supplement',
  'fellas',
  'gigi-s',
  'gnc',
  'grizzone',
  'hardline',
  'heyday',
  'hiq',
  'imperium-supplements',
  'kiperin',
  'mla-protein',
  'nois-nutrition',
  'prime-nutrition',
  'proteinocean',
  's4u-nutrition',
  'space-gym-supplements',
  'ssn',
  'supplement-factory',
  'supra-protein',
  'swiss-nutrition',
  'think-nutrition',
  'torq-nutrition',
  'vitabear',
  'west-nutrition',
  'yesilmarka',
]);

/**
 * Şeffaf zeminli, AÇIK RENK logolar. Kartın açık gri dairesinde (#f7f7fa)
 * bunlar tamamen kayboluyor — Prime Nutrition kartı bomboş görünüyordu.
 *
 * Liste tahminle değil ÖLÇÜLEREK çıkarıldı: her dosyanın saydam olmayan
 * pikselleri üzerinden ortalama parlaklık ve saydamlık oranı hesaplandı,
 * "parlaklık > 200 ve saydamlık > %20" olanlar alındı. Saydam olmayan açık
 * logolar (GNC, Think Nutrition, Imperium) listede YOK — onların kendi açık
 * zemini var, dairede zaten düzgün görünüyorlar.
 */
const KOYU_ZEMIN_ISTEYEN: ReadonlySet<string> = new Set([
  'prime-nutrition',
  'space-gym-supplements',
  'supplement-factory',
]);

/** Logo dosyası varsa yolu, yoksa null. */
export function brandLogoUrl(brandName: string): string | null {
  const slug = brandSlug(brandName);
  return YEREL_LOGOLU_MARKALAR.has(slug) ? `/marka-logo/${slug}.webp` : null;
}

/** Logo açık renk + şeffaf zeminliyse kart dairesi koyulaştırılmalı. */
export function brandLogoNeedsDarkBackdrop(brandName: string): boolean {
  return KOYU_ZEMIN_ISTEYEN.has(brandSlug(brandName));
}

/**
 * Logosu olmayan markalar için monogram harfleri.
 *
 * Genel bir mağaza ikonu yerine monogram kullanılıyor: 89 markanın 66'sında
 * aynı ikon tekrarlanınca dizin "eksik" görünüyordu ve markalar birbirinden
 * ayırt edilemiyordu. Monogram uydurma bilgi DEĞİL — markanın kendi adından
 * türüyor.
 *
 * İki kelimeli adlarda iki kelimenin baş harfi ("Nuclear Nutrition" -> "NN"),
 * tek kelimede ilk iki harf ("Olimp" -> "OL").
 */
export function brandMonogram(brandName: string): string {
  const kelimeler = brandName
    .split(/[\s.&-]+/)
    .filter((k) => /[a-zA-ZçğıöşüÇĞİÖŞÜ0-9]/.test(k));

  if (kelimeler.length === 0) return '?';
  if (kelimeler.length === 1) return kelimeler[0].slice(0, 2).toLocaleUpperCase('tr-TR');

  return (kelimeler[0][0] + kelimeler[1][0]).toLocaleUpperCase('tr-TR');
}

// Monogram zeminleri — Nocturne paletiyle uyumlu, okunabilirliği test edilmiş
// koyu tonlar (üzerine beyaz yazı geliyor).
const MONOGRAM_RENKLERI = [
  '#4c4bb8',
  '#2f6f5e',
  '#8a4b7d',
  '#a15c2f',
  '#3a6b96',
  '#7a3f52',
  '#4d6b2f',
  '#6b4a94',
];

/**
 * Marka adından SABİT bir zemin rengi. Aynı marka her zaman aynı rengi alır
 * (rastgele değil), böylece kullanıcı markayı renginden de tanıyabiliyor.
 */
export function brandMonogramColor(brandName: string): string {
  let toplam = 0;
  for (let i = 0; i < brandName.length; i++) {
    toplam = (toplam * 31 + brandName.charCodeAt(i)) >>> 0;
  }
  return MONOGRAM_RENKLERI[toplam % MONOGRAM_RENKLERI.length];
}

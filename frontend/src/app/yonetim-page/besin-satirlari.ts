// Yönetim panelindeki besin düzenleyicisinin makro dışı satırları. Kreatin, amino
// asit, pre-workout ve vitamin etiketleri kalorisiz, adlı satırlar basıyor:
// "Kreatin Monohidrat 5 g", "Kafein 200 mg". Her satırı backend kontrol ediyor
// (ManualProductDataService.Kontrol); bu dosya yalnızca formu şekillendiriyor.

/** Backend'in birim listesi, aynı yazımla. */
export const BESIN_BIRIMLERI = ['g', 'mg', 'mcg', 'IU', 'milyar CFU'] as const;

export interface BesinSatiriFormu {
  ad: string;
  /** Makro alanları gibi kaydedilene kadar metin. */
  miktar: string;
  birim: string;
}

export type MakroAlani = 'porsiyon' | 'enerji' | 'yag' | 'karbonhidrat' | 'lif' | 'protein';

/**
 * Türkçe harf katlama: tr-TR "PROTEIN"i "proteın" yapar, invariant "LİF"i
 * "lİf" bırakır; ikisini de elle aynı biçime getiriyoruz (backend'deki Katla).
 */
export function katla(metin: string): string {
  return metin.replace(/İ/g, 'i').replace(/I/g, 'i').replace(/ı/g, 'i').toLowerCase().trim();
}

// Otomatik okumalar satır adını çoğu zaman iki dilde yazıyor ("Yağ / Fat",
// "-Şekerler/Sugars"). Eşleştirme yalnızca ilk parçaya bakıyor.
function anaAd(ad: string): string {
  return katla(ad.split('/')[0].replace(/^[-–\s]+/, '')).replace(/\s+/g, ' ');
}

// Düzenleyicide kendi alanı olan satırlar. Backend bunları serbest satır olarak
// reddediyor; o yüzden hiçbir zaman serbest satır diye sunulmuyor ya da okunmuyor.
const MAKRO_ADLARI: Record<string, MakroAlani> = {
  porsiyon: 'porsiyon',
  enerji: 'enerji',
  kalori: 'enerji',
  yag: 'yag',
  'yağ': 'yag',
  'toplam yağ': 'yag',
  karbonhidrat: 'karbonhidrat',
  karbonhidratlar: 'karbonhidrat',
  'toplam karbonhidrat': 'karbonhidrat',
  lif: 'lif',
  'diyet lifi': 'lif',
  protein: 'protein',
};

export function makroAlani(ad: string): MakroAlani | null {
  return MAKRO_ADLARI[anaAd(ad)] ?? null;
}

type SablonSatiri = { ad: string; birim: (typeof BESIN_BIRIMLERI)[number] };

const GIDA_SATIRLARI: SablonSatiri[] = [
  { ad: 'Doymuş Yağ', birim: 'g' },
  { ad: 'Şekerler', birim: 'g' },
  { ad: 'Tuz', birim: 'g' },
];

/**
 * Kategorinin etiketlerinde genellikle geçen satırlar, başlangıç noktası olarak.
 * Miktar her zaman etiketten yazılıyor; şablon hiçbir miktarı doldurmuyor.
 */
export const SATIR_SABLONLARI: Record<string, SablonSatiri[]> = {
  'protein-tozu': GIDA_SATIRLARI,
  'kilo-hacim': GIDA_SATIRLARI,
  'saglikli-atistirmaliklar': GIDA_SATIRLARI,
  kreatin: [{ ad: 'Kreatin Monohidrat', birim: 'g' }],
  'amino-asitler': [
    { ad: 'L-Lösin', birim: 'g' },
    { ad: 'L-İzolösin', birim: 'g' },
    { ad: 'L-Valin', birim: 'g' },
    { ad: 'L-Glutamin', birim: 'g' },
  ],
  'pre-workout': [
    { ad: 'Kafein', birim: 'mg' },
    { ad: 'L-Sitrülin', birim: 'g' },
    { ad: 'Beta Alanin', birim: 'g' },
    { ad: 'Betain', birim: 'g' },
  ],
  'yag-yakici': [
    { ad: 'Kafein', birim: 'mg' },
    { ad: 'L-Karnitin', birim: 'mg' },
    { ad: 'Yeşil Çay Ekstresi', birim: 'mg' },
  ],
  'l-carnitine-cla': [
    { ad: 'L-Karnitin', birim: 'mg' },
    { ad: 'CLA', birim: 'mg' },
  ],
  vitamin: [
    { ad: 'Vitamin A', birim: 'mcg' },
    { ad: 'Vitamin C', birim: 'mg' },
    { ad: 'Vitamin D3', birim: 'mcg' },
    { ad: 'Vitamin E', birim: 'mg' },
    { ad: 'Çinko', birim: 'mg' },
    { ad: 'Magnezyum', birim: 'mg' },
  ],
};

/** Öneri listesi için her şablon adı bir kez. */
export const SATIR_ADI_ONERILERI: string[] = [
  ...new Set(Object.values(SATIR_SABLONLARI).flatMap((satirlar) => satirlar.map((s) => s.ad))),
].sort((a, b) => a.localeCompare(b, 'tr'));

// "5 g", "0,86 g", "0 gr", "25 mcg", "3000 IU", "10 milyar CFU": backend'in yazdığı
// ve otomatik okumaların sık kullandığı yazımlar. Virgül ondalık, "gr" gram.
const KAYITLI_MIKTAR = /^(\d+(?:[.,]\d+)?)\s*(g|gr|mg|mcg|µg|iu|milyar cfu)\.?$/i;

function birimBul(ham: string): string | null {
  const kucuk = ham.toLowerCase();
  if (kucuk === 'gr') return 'g';
  if (kucuk === 'µg') return 'mcg';
  return BESIN_BIRIMLERI.find((b) => b.toLowerCase() === kucuk) ?? null;
}

/**
 * Makro alanının kayıtlı değerini sayı metni olarak döndürür. "0 kj / 0 kcal"
 * gibi enerji yazımlarında kcal'lik sayı alınıyor, kJ değil.
 */
export function makroDegeri(tablo: Record<string, string>, alan: MakroAlani): string {
  const satir = Object.entries(tablo).find(([ad]) => makroAlani(ad) === alan);
  if (!satir) return '';
  const deger = satir[1];
  const sayi =
    (alan === 'enerji' ? deger.match(/(\d+(?:[.,]\d+)?)\s*kcal/i)?.[1] : null) ??
    deger.match(/\d+(?:[.,]\d+)?/)?.[0] ??
    '';
  return sayi.replace(',', '.');
}

/**
 * Kayıtlı tabloyu düzenlenebilir satırlara ayırır. Başka biçimdeki satırlar
 * (otomatik okumanın "%10"u) burada düzenlenemiyor; sessizce düşürmek yerine
 * adlarıyla döndürülüyor ki düzenleyici kaydedince kalmayacaklarını söylesin.
 */
export function kayitliDigerSatirlar(tablo: Record<string, string>): {
  satirlar: BesinSatiriFormu[];
  atlananlar: string[];
} {
  const satirlar: BesinSatiriFormu[] = [];
  const atlananlar: string[] = [];

  for (const [ad, deger] of Object.entries(tablo)) {
    if (makroAlani(ad)) continue;
    const eslesme = deger.trim().match(KAYITLI_MIKTAR);
    const birim = eslesme && birimBul(eslesme[2]);
    if (eslesme && birim) {
      satirlar.push({ ad, miktar: eslesme[1].replace(',', '.'), birim });
    } else {
      atlananlar.push(ad);
    }
  }

  return { satirlar, atlananlar };
}

// Otomatik okumalar aynı satırı farklı adlarla yazıyor ("Şeker" / "Şekerler");
// şablon bunları ayrı satır sanıp ikinci kez eklemesin.
const ESANLAMLI_ADLAR: Record<string, string> = { 'şekerler': 'şeker' };

function sablonAnahtari(ad: string): string {
  const ana = anaAd(ad);
  return ESANLAMLI_ADLAR[ana] ?? ana;
}

/** Kategorinin formda henüz olmayan (ada göre) şablon satırları. */
export function eklenecekSablonSatirlari(
  kategori: string | null,
  mevcut: BesinSatiriFormu[],
): BesinSatiriFormu[] {
  const adlar = new Set(mevcut.map((s) => sablonAnahtari(s.ad)));
  return (SATIR_SABLONLARI[kategori ?? ''] ?? [])
    .filter((s) => !adlar.has(sablonAnahtari(s.ad)))
    .map((s) => ({ ad: s.ad, miktar: '', birim: s.birim }));
}

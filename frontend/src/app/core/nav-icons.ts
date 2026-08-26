// Nav dropdown'larındaki (Kategoriler, Hesaplama) ve hesaplama araçları
// index sayfasındaki kartların paylaştığı ikon path'leri — kullanıcı
// geri bildirimi: dropdown'lar sadece düz metin listesiydi, kelimenin
// başına alakalı bir sembol eklenmesi istendi. Tek yerde tutulup her iki
// yerde de (site-header.ts, deals-list.ts, calculator-list-page.ts)
// aynı path kullanılıyor — ikon seti tutarlı kalsın diye.
export const CATEGORY_ICON_PATHS: Record<string, string> = {
  'protein-tozu': 'M6 4h12M7.5 4v4L4 19a1.5 1.5 0 0 0 1.4 2h13.2a1.5 1.5 0 0 0 1.4-2L16.5 8V4',
  kreatin: 'M13 2 4 14h6l-1 8 9-12h-6l1-8Z',
  'amino-asitler': 'M9 3h6M10 3v5.5L4.5 18a2 2 0 0 0 1.8 3h11.4a2 2 0 0 0 1.8-3L14 8.5V3',
  'pre-workout': 'M3 12h4l2-7 4 14 2-7h6',
  'yag-yakici': 'M12 3c-1 3-4 4-4 8a4 4 0 0 0 8 0c0-1-.5-1.5-1-2 .3 1.5-.5 2.5-1.5 2.5-1.4 0-2-1.2-1.2-2.7C13 7 12.6 5 12 3Z',
  'kilo-hacim': 'M3 17l6-6 4 4 8-8M14 7h7v7',
  vitamin: 'M12 2l2.9 6.3 6.9.6-5.2 4.6 1.6 6.8L12 16.9l-6.2 3.4 1.6-6.8L3.2 8.9l6.9-.6Z',
  'saglikli-atistirmaliklar': 'M12 21s-7-4.4-9.5-9A5.5 5.5 0 0 1 12 6a5.5 5.5 0 0 1 9.5 6C19 16.6 12 21 12 21Z',
  'l-carnitine-cla': 'M12 3s6 7 6 11.5A6 6 0 0 1 6 14.5C6 10 12 3 12 3Z',
};
export const DEFAULT_CATEGORY_ICON = 'M4 6h16M4 12h16M4 18h16';

// Phosphor ikonları ana sayfadaki kategori şeridi ve paylaşılan header
// dropdown'ında ortak kullanılıyor. Görsel path listesinden ayrı tutuluyor;
// yeni arayüz font ikonlarını, eski hesaplama kartları ise path'leri kullanıyor.
export const CATEGORY_PHOSPHOR_ICONS: Record<string, string> = {
  'protein-tozu': 'ph-jar',
  'amino-asitler': 'ph-share-network',
  kreatin: 'ph-lightning',
  'pre-workout': 'ph-gauge',
  'protein-bar': 'ph-cookie',
  'yag-yakici': 'ph-fire',
  'kilo-hacim': 'ph-barbell',
  vitamin: 'ph-shield-plus',
  'saglikli-atistirmaliklar': 'ph-leaf',
  'l-carnitine-cla': 'ph-drop-half',
};

export const DEFAULT_CATEGORY_PHOSPHOR_ICON = 'ph-dots-three';

export function categoryPhosphorIcon(slug: string): string {
  return CATEGORY_PHOSPHOR_ICONS[slug] ?? DEFAULT_CATEGORY_PHOSPHOR_ICON;
}

export type CalculatorIcon = 'plate' | 'capsule' | 'flame' | 'ruler' | 'droplet';

export const CALCULATOR_ICON_PATHS: Record<CalculatorIcon, string> = {
  plate: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18ZM12 8v4l3 2',
  capsule: 'M6.5 6.5 17.5 17.5M8.6 4.4a5 5 0 0 1 7 7l-4.2 4.2a5 5 0 0 1-7-7Z',
  flame: 'M12 3c-1 3-4 4-4 8a4 4 0 0 0 8 0c0-1-.5-1.5-1-2 .3 1.5-.5 2.5-1.5 2.5-1.4 0-2-1.2-1.2-2.7C13 7 12.6 5 12 3Z',
  ruler: 'M4 15 15 4l5 5-11 11ZM8 11l2 2M11 8l2 2M14 5l2 2',
  droplet: 'M12 3s6 7 6 11.5A6 6 0 0 1 6 14.5C6 10 12 3 12 3Z',
};

// Vücut hesaplayıcılarının slug'ına göre ikon — protein ihtiyacı 'plate',
// takviye dozu hesaplayıcılarının hepsi 'capsule' (aşağıda ayrıca ele alınıyor).
const BODY_CALCULATOR_ICON_BY_SLUG: Record<string, CalculatorIcon> = {
  'kalori-ihtiyaci': 'flame',
  'vucut-kitle-indeksi': 'ruler',
  'gunluk-su-ihtiyaci': 'droplet',
};

// Hesaplayıcı path'i "/hesaplama/{slug}" için doğru ikonu döner — protein
// ihtiyacı ve vücut hesaplayıcıları kendi ikonunu taşır, takviye dozu
// hesaplayıcılarının (kreatin, beta-alanine vb.) hepsi 'capsule' kullanır.
export function calculatorIconPath(slug: string): string {
  if (slug === 'protein-ihtiyaci') return CALCULATOR_ICON_PATHS.plate;
  const bodyIcon = BODY_CALCULATOR_ICON_BY_SLUG[slug];
  return CALCULATOR_ICON_PATHS[bodyIcon ?? 'capsule'];
}

// Menüde her araç kendi işlevini ilk bakışta anlatır: beslenme, enerji,
// ölçüm, su ve takviye türleri aynı hesap makinesi simgesini paylaşmaz.
const CALCULATOR_PHOSPHOR_ICON_BY_SLUG: Record<string, string> = {
  'protein-ihtiyaci': 'ph-bowl-food',
  'kalori-ihtiyaci': 'ph-fire',
  'vucut-kitle-indeksi': 'ph-scales',
  'gunluk-su-ihtiyaci': 'ph-drop',
  'kreatin-dozu': 'ph-lightning',
  'beta-alanine-dozu': 'ph-waves',
  'sitrulin-dozu': 'ph-heartbeat',
  'betain-dozu': 'ph-drop-half-bottom',
  'eaa-dozu': 'ph-share-network',
};

export function calculatorPhosphorIcon(slug: string): string {
  return CALCULATOR_PHOSPHOR_ICON_BY_SLUG[slug] ?? 'ph-pill';
}

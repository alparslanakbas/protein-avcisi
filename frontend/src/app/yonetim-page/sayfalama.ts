/**
 * Şablondaki ortak sayfalama parçasının (`#sayfalama`) girdisi. Ürün, marka ve abone
 * listesi aynı parçayı kullanıyor (4 Ekim; önceden üç kopya vardı ve şablon boyut
 * tavanındaydı).
 */
export interface Sayfalama {
  /** "1–50 / 230 satır" gibi aralık metni. */
  ozet: string;
  sayfa: number;
  toplamSayfa: number;
  sayfalar: number[];
  /** İstek sürerken düğmeler kilitli. */
  mesgul: boolean;
  git: (sayfa: number) => void;
}

/** Görünen en fazla beş sayfa numarası; mevcut sayfa mümkünse ortada. */
export function gorunenSayfalar(sayfa: number, toplamSayfa: number): number[] {
  const baslangic = Math.min(Math.max(1, sayfa - 2), Math.max(1, toplamSayfa - 4));
  return Array.from({ length: Math.min(5, toplamSayfa) }, (_, index) => baslangic + index);
}

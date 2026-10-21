import { CATEGORY_LABELS } from './category-labels';

// Aylık fiyat endeksi (backend FiyatEndeksiServisi). Aylar en yeniden eskiye; endeks Eylül 2026 = 100'den zincirleme,
// zincir kopan kategoride null.
export interface FiyatEndeksiKategorisi {
  kategori: string;
  endeks: number | null;
  aylikDegisimYuzde: number;
  urunSayisi: number;
  markaSayisi: number;
  artanPayi: number;
  azalanPayi: number;
  dislanan: number;
}

export interface FiyatEndeksiAyi {
  /** "2026-10-01" */
  ay: string;
  gecici: boolean;
  hesaplandi: string;
  kategoriler: FiyatEndeksiKategorisi[];
}

export interface FiyatEndeksi {
  tabanAy: string;
  aylar: FiyatEndeksiAyi[];
}

/** Bütün kategorilerin birlikte hesaplandığı satır (backend PriceIndexMonth.AllCategories). */
export const TUM_KATEGORILER = 'tum';

const AYLAR = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'];

// Yönelme hâli eki ay başına sabit (ünlü uyumu), kuralla üretmeye gerek yok.
const AYA = ["Ocak'a", "Şubat'a", "Mart'a", "Nisan'a", "Mayıs'a", "Haziran'a", "Temmuz'a", "Ağustos'a", "Eylül'e", "Ekim'e", "Kasım'a", "Aralık'a"];

function parca(ay: string): { yil: number; ay: number } {
  const [yil, aySirasi] = ay.split('-').map(Number);
  return { yil, ay: aySirasi };
}

/** "2026-10-01" → "Ekim 2026" */
export function ayAdi(ay: string): string {
  const p = parca(ay);
  return `${AYLAR[p.ay - 1]} ${p.yil}`;
}

/** Bir önceki ay, yönelme ekiyle: "2026-10-01" → "Eylül'e" (cümle: "Eylül'e göre"). */
export function oncekiAyaGore(ay: string): string {
  const p = parca(ay);
  return AYA[(p.ay + 10) % 12];
}

const yuzdeBicimi = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** "+%1,41", "−%0,01", "%0,00". Türkçede yüzde işareti sayıdan önce. */
export function degisimMetni(yuzde: number): string {
  const mutlak = yuzdeBicimi.format(Math.abs(yuzde));
  if (yuzde > 0) return `+%${mutlak}`;
  if (yuzde < 0) return `−%${mutlak}`;
  return `%${mutlak}`;
}

export function kategoriAdi(kategori: string): string {
  return kategori === TUM_KATEGORILER ? 'Bütün kategoriler' : (CATEGORY_LABELS[kategori] ?? kategori);
}

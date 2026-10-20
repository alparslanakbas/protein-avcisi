// Marka karşılaştırma sayfasının yorum cümleleri: hepsi o iki markanın kendi
// verisinden (kilogram fiyatı medyanı, gerçek indirim, son 30 günün fiyat hareketi).
//
// "Daha ucuz" kararı YALNIZ kilogram fiyatıyla veriliyor (20 Ekim). Ham ortalama
// paket boyutuna bakmıyordu: 300 g'lık ürünleri çok olan marka, 5 kg satan rakibinden
// "ucuz" görünebiliyordu. Kapsül/tablet kategorilerinde kilogram fiyatı yok; orada
// ortalama yine gösteriliyor ama kazanan ilan edilmiyor.
//
// Marka adlarına ek getirilmiyor ("X'in" yerine "X ürünlerinde"): ünlü uyumu ve
// kısaltmalar (SSN, HIQ) yüzünden doğru eki otomatik seçmek güvenilir değil.

import { BrandComparison, CategoryComparison, PriceActivity } from './brand-comparison.model';
import { BrandStats } from './brand-stats.model';
import { formatPercent } from './meta-description';

/** Göreli farkı bundan küçükse iki kilogram fiyatı "eşit" sayılır. */
const ESITLIK_ESIGI = 0.01;

export function cheaperByKg(cat: CategoryComparison): 1 | 2 | null {
  const a = cat.brand1MedianPerKg;
  const b = cat.brand2MedianPerKg;
  if (a === null || b === null || a <= 0 || b <= 0) return null;
  if (Math.abs(a - b) / Math.max(a, b) < ESITLIK_ESIGI) return null;
  return a < b ? 1 : 2;
}

export interface KgSummary {
  /** İki markada da kilogram fiyatı olan ortak kategori sayısı. */
  comparable: number;
  leader: 1 | 2 | null;
  leaderWins: number;
  ties: number;
  biggestDiff: { category: string; percent: number; cheaper: 1 | 2 } | null;
}

export function kgSummary(categories: CategoryComparison[]): KgSummary {
  let wins1 = 0;
  let wins2 = 0;
  let comparable = 0;
  let biggestDiff: KgSummary['biggestDiff'] = null;

  for (const cat of categories) {
    if (cat.brand1MedianPerKg === null || cat.brand2MedianPerKg === null) continue;
    comparable++;
    const winner = cheaperByKg(cat);
    if (winner === null) continue;
    if (winner === 1) wins1++;
    else wins2++;

    const higher = Math.max(cat.brand1MedianPerKg, cat.brand2MedianPerKg);
    const lower = Math.min(cat.brand1MedianPerKg, cat.brand2MedianPerKg);
    const percent = Math.round(((higher - lower) / higher) * 100);
    if (!biggestDiff || percent > biggestDiff.percent) {
      biggestDiff = { category: cat.category, percent, cheaper: winner };
    }
  }

  const leader: 1 | 2 | null = wins1 === wins2 ? null : wins1 > wins2 ? 1 : 2;
  return {
    comparable,
    leader,
    leaderWins: leader === 1 ? wins1 : leader === 2 ? wins2 : 0,
    ties: comparable - wins1 - wins2,
    biggestDiff,
  };
}

function activityPart(brand: string, a: PriceActivity | null): string | null {
  if (!a) return null;
  if (a.drops === 0 && a.rises === 0) return `${brand} ürünlerinde fiyat hiç değişmedi`;
  return `${brand} ürünlerinde ${a.drops} fiyat düşüşü ve ${a.rises} fiyat artışı kaydettik (${a.changedProducts} üründe)`;
}

/** Son 30 günün fiyat hareketi; veri yoksa null. */
export function activitySentence(c: BrandComparison): string | null {
  const a1 = c.brand1Activity;
  const a2 = c.brand2Activity;
  if (!a1 || !a2) return null;
  if (a1.drops + a1.rises + a2.drops + a2.rises === 0) {
    return 'Son 30 günde iki markanın ürünlerinde de fiyat değişmedi.';
  }
  return `Son 30 günde ${activityPart(c.brand1, a1)}; ${activityPart(c.brand2, a2)}.`;
}

function discountPart(brand: string, s: BrandStats): string {
  if (s.discountCount === 0) return `${brand} ürünlerinde gerçek indirim yok`;
  const avg = s.averageDiscountPercent;
  // Yuvarlanınca 0 görünen çok küçük indirim "ortalama %0" diye yazılmasın.
  const depth = avg === null ? '' : avg < 1 ? " (ortalama %1'in altında)" : ` (ortalama %${formatPercent(avg)})`;
  return `${brand} ürünlerinden ${s.discountCount} tanesi gerçek indirimde${depth}`;
}

/** Bugünkü gerçek indirim durumu (marka sayfasıyla aynı hesap); veri yoksa null. */
export function discountSentence(c: BrandComparison): string | null {
  const s1 = c.brand1Stats;
  const s2 = c.brand2Stats;
  if (!s1 || !s2) return null;
  if (s1.discountCount === 0 && s2.discountCount === 0) {
    return 'Bugün iki markanın ürünlerinde de gerçek indirim yok: güncel fiyatlar olağan fiyatlarının altında değil.';
  }
  return `Bugün itibarıyla ${discountPart(c.brand1, s1)}; ${discountPart(c.brand2, s2)}.`;
}

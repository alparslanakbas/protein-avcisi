// Marka sayfasının "fiyat konumu" metinleri (20 Ekim). Marka adına ek getirilmiyor
// ("X ürünlerinde"), bkz. brand-comparison-text.ts.

import { PriceActivity } from './brand-comparison.model';
import { BrandKgPosition } from './brand-insights.model';

/** Göreli farkı bundan küçükse "fark yok" denir (karşılaştırma sayfasıyla aynı eşik). */
const ESIT_YUZDE = 1;

export function brandActivitySentence(brand: string, a: PriceActivity): string {
  if (a.drops === 0 && a.rises === 0) return `Son 30 günde ${brand} ürünlerinde fiyat hiç değişmedi.`;
  return `Son 30 günde ${brand} ürünlerinde ${a.drops} fiyat düşüşü ve ${a.rises} fiyat artışı kaydettik (${a.changedProducts} üründe).`;
}

/** Markanın kilogram fiyatı, bütün markalarınkinden yüzde kaç uygun/pahalı; hesaplanamıyorsa null. */
export function kgPositionText(p: BrandKgPosition): string | null {
  const m = p.brandMedianPerKg;
  const piyasa = p.marketMedianPerKg;
  if (m === null || piyasa === null || piyasa <= 0) return null;
  const yuzde = Math.round(((m - piyasa) / piyasa) * 100);
  if (Math.abs(yuzde) < ESIT_YUZDE) return 'fark yok';
  return yuzde < 0 ? `%${-yuzde} daha uygun` : `%${yuzde} daha pahalı`;
}

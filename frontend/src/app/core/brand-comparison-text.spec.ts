import { describe, expect, it } from 'vitest';

import { BrandComparison, CategoryComparison } from './brand-comparison.model';
import { BrandStats } from './brand-stats.model';
import { activitySentence, cheaperByKg, discountSentence, kgSummary } from './brand-comparison-text';

function kat(category: string, kg1: number | null, kg2: number | null, avg1 = 500, avg2 = 900): CategoryComparison {
  return {
    category,
    brand1AvgPrice: avg1, brand1Count: 3, brand2AvgPrice: avg2, brand2Count: 3,
    brand1MedianPerKg: kg1, brand1KgCount: kg1 === null ? 0 : 3, brand1BestPick: null,
    brand2MedianPerKg: kg2, brand2KgCount: kg2 === null ? 0 : 3, brand2BestPick: null,
  };
}

function stats(discountCount: number, averageDiscountPercent: number | null): BrandStats {
  return { totalProducts: 10, discountCount, thirtyDayLowCount: 0, averageDiscountPercent, lastScanAt: null, averagePrice: null };
}

function karsilastirma(over: Partial<BrandComparison>): BrandComparison {
  return {
    brand1: 'BigJoy', brand2: 'SSN', brand1TotalProducts: 10, brand2TotalProducts: 10, categories: [],
    brand1Activity: null, brand2Activity: null, brand1Stats: null, brand2Stats: null, ...over,
  };
}

describe('cheaperByKg', () => {
  it('ham ortalama değil kilogram fiyatı karar verir', () => {
    // Ortalamada marka 1 ucuz (500 < 900) ama kilogramda pahalı.
    expect(cheaperByKg(kat('protein-tozu', 1600, 1200))).toBe(2);
  });

  it('kilogram fiyatı olmayan kategoride kazanan yok', () => {
    expect(cheaperByKg(kat('vitamin', null, 1200))).toBeNull();
    expect(cheaperByKg(kat('vitamin', null, null))).toBeNull();
  });

  it('yüzde birden küçük fark eşit sayılır', () => {
    expect(cheaperByKg(kat('kreatin', 1000, 1005))).toBeNull();
  });
});

describe('kgSummary', () => {
  it('yalnız iki tarafta da kilogram fiyatı olan kategorileri sayar', () => {
    const s = kgSummary([kat('protein-tozu', 1600, 1200), kat('kreatin', 900, 1000), kat('amino-asitler', 800, 1000), kat('vitamin', null, null)]);
    expect(s.comparable).toBe(3);
    expect(s.leader).toBe(1);
    expect(s.leaderWins).toBe(2);
    expect(s.ties).toBe(0);
    expect(s.biggestDiff).toEqual({ category: 'protein-tozu', percent: 25, cheaper: 2 });
  });

  it('karşılaştırılabilir kategori yoksa lider yok', () => {
    const s = kgSummary([kat('vitamin', null, 500)]);
    expect(s.comparable).toBe(0);
    expect(s.leader).toBeNull();
  });
});

describe('activitySentence', () => {
  it('iki markanın hareketini marka adına ek getirmeden yazar', () => {
    const t = activitySentence(karsilastirma({
      brand1Activity: { drops: 248, rises: 345, changedProducts: 120 },
      brand2Activity: { drops: 0, rises: 0, changedProducts: 0 },
    }));
    expect(t).toBe('Son 30 günde BigJoy ürünlerinde 248 fiyat düşüşü ve 345 fiyat artışı kaydettik (120 üründe); SSN ürünlerinde fiyat hiç değişmedi.');
  });

  it('ikisi de sabitse tek cümle', () => {
    const t = activitySentence(karsilastirma({
      brand1Activity: { drops: 0, rises: 0, changedProducts: 0 },
      brand2Activity: { drops: 0, rises: 0, changedProducts: 0 },
    }));
    expect(t).toBe('Son 30 günde iki markanın ürünlerinde de fiyat değişmedi.');
  });

  it('veri yoksa null', () => {
    expect(activitySentence(karsilastirma({}))).toBeNull();
  });
});

describe('discountSentence', () => {
  it('Türkçe yüzde biçimiyle yazar', () => {
    const t = discountSentence(karsilastirma({ brand1Stats: stats(21, 12.5), brand2Stats: stats(0, null) }));
    expect(t).toBe('Bugün itibarıyla BigJoy ürünlerinden 21 tanesi gerçek indirimde (ortalama %12,5); SSN ürünlerinde gerçek indirim yok.');
  });

  it('yüzde birin altındaki ortalamayı %0 diye yazmaz', () => {
    const t = discountSentence(karsilastirma({ brand1Stats: stats(1, 0), brand2Stats: stats(2, 0.4) }));
    expect(t).toBe("Bugün itibarıyla BigJoy ürünlerinden 1 tanesi gerçek indirimde (ortalama %1'in altında); SSN ürünlerinden 2 tanesi gerçek indirimde (ortalama %1'in altında).");
  });

  it('ikisinde de indirim yoksa açıklar', () => {
    expect(discountSentence(karsilastirma({ brand1Stats: stats(0, null), brand2Stats: stats(0, null) })))
      .toContain('iki markanın ürünlerinde de gerçek indirim yok');
  });
});

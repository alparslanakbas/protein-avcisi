import { describe, expect, it } from 'vitest';

import { BrandKgPosition } from './brand-insights.model';
import { brandActivitySentence, kgPositionText } from './brand-insights-text';

function konum(marka: number | null, piyasa: number | null): BrandKgPosition {
  return {
    category: 'kreatin', brandMedianPerKg: marka, brandKgCount: 3, marketMedianPerKg: piyasa, marketKgCount: 150,
    marketBrandCount: 40, bestPick: { productId: 1, productName: 'Kreatin 500 Gr', size: '500 Gr', price: 700, pricePerKg: 1400 },
  };
}

describe('brandActivitySentence', () => {
  it('hareketi marka adına ek getirmeden yazar', () => {
    expect(brandActivitySentence('HIQ', { drops: 14, rises: 4, changedProducts: 17 }))
      .toBe('Son 30 günde HIQ ürünlerinde 14 fiyat düşüşü ve 4 fiyat artışı kaydettik (17 üründe).');
  });

  it('değişim yoksa açıkça söyler', () => {
    expect(brandActivitySentence('West Nutrition', { drops: 0, rises: 0, changedProducts: 0 }))
      .toBe('Son 30 günde West Nutrition ürünlerinde fiyat hiç değişmedi.');
  });
});

describe('kgPositionText', () => {
  it('bütün markalara göre yönü ve yüzdeyi verir', () => {
    expect(kgPositionText(konum(1413.83, 2870.7))).toBe('%51 daha uygun');
    expect(kgPositionText(konum(6888.89, 2870.7))).toBe('%140 daha pahalı');
  });

  it('yüzde birin altındaki farkı "fark yok" sayar', () => {
    expect(kgPositionText(konum(793.8, 793.8))).toBe('fark yok');
  });

  it('hesaplanamıyorsa null', () => {
    expect(kgPositionText(konum(null, 2870.7))).toBeNull();
  });
});

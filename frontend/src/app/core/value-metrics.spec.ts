import { describe, expect, it } from 'vitest';
import { Deal } from './deal.model';
import {
  PROTEIN_REFERENCE_GRAMS,
  pricePerServing,
  proteinRatioPercent,
  proteinReferenceCost,
  servingsInPackage,
} from './value-metrics';

// Değerler gerçek üretim verisinden alındı (28 Ağustos 2026):
// SSN WPC-80 → 20 g protein / 25 g porsiyon = %80
// HIQ Caseinight → 22,5 g / 30 g = %75
// HIQ High Pro Protein Bar → 16 g / 55 g = %29
function deal(overrides: Partial<Deal> = {}): Deal {
  return {
    productId: 1,
    productName: 'Test',
    productUrl: 'https://x',
    imageUrl: null,
    category: null,
    size: '900 Gr',
    flavor: null,
    servingSizeGrams: 30,
    servingsPerPackage: null,
    description: null,
    nutritionJson: null,
    proteinPerServingGrams: 24,
    brandName: 'Test',
    currentPrice: 1000,
    referencePrice: 1000,
    discountPercent: 0,
    storeOldPrice: null,
    storeDiscountPercent: null,
    scrapedAt: new Date().toISOString(),
    isAtThirtyDayLow: false,
    ...overrides,
  } as Deal;
}

describe('servingsInPackage', () => {
  it('markanın beyan ettiği porsiyon sayısını önceliklendirir', () => {
    // Paket etiketinden hesaplanabilecek olsa bile markanın beyanı kazanır.
    expect(servingsInPackage(deal({ servingsPerPackage: 64, size: '900 Gr' }))).toBe(64);
  });

  it('beyan yoksa paket ağırlığını porsiyona böler', () => {
    expect(servingsInPackage(deal({ size: '900 Gr', servingSizeGrams: 30 }))).toBe(30);
  });

  it('kilogramla verilen paketi de grama çevirir', () => {
    // Backend bunu zaten yapıyordu, frontend yalnızca "Gr" tanıyordu —
    // kg ile satılan her ürün sessizce hesap dışında kalıyordu.
    expect(servingsInPackage(deal({ size: '2 Kg', servingSizeGrams: 30 }))).toBeCloseTo(66.67, 1);
    expect(servingsInPackage(deal({ size: '1,5 kg', servingSizeGrams: 30 }))).toBe(50);
  });

  it('paket ağırlığı okunamıyorsa null döner', () => {
    // ProteinOcean gibi bazı markalarda paket bilgisi hiç gelmiyor.
    expect(servingsInPackage(deal({ size: null }))).toBeNull();
    expect(servingsInPackage(deal({ size: '30 Kapsül' }))).toBeNull();
  });
});

describe('proteinRatioPercent', () => {
  it('gerçek ürün değerlerini doğru hesaplar', () => {
    expect(proteinRatioPercent(deal({ proteinPerServingGrams: 20, servingSizeGrams: 25 }))).toBe(80);
    expect(proteinRatioPercent(deal({ proteinPerServingGrams: 22.5, servingSizeGrams: 30 }))).toBe(75);
    expect(proteinRatioPercent(deal({ proteinPerServingGrams: 16, servingSizeGrams: 55 }))).toBe(29);
  });

  // Veri yoksa TAHMİN ETMİYORUZ — rakiplerden biri porsiyonu bilmediğinde
  // standart 30 gram varsayıyor, biz alanı hiç göstermiyoruz.
  it('protein veya porsiyon eksikse null döner', () => {
    expect(proteinRatioPercent(deal({ proteinPerServingGrams: null }))).toBeNull();
    expect(proteinRatioPercent(deal({ servingSizeGrams: null }))).toBeNull();
  });

  it('etiket verisi tutarsızsa null döner', () => {
    // Porsiyondan fazla protein olamaz; böyle bir değer ayrıştırma hatasıdır.
    expect(proteinRatioPercent(deal({ proteinPerServingGrams: 40, servingSizeGrams: 30 }))).toBeNull();
    expect(proteinRatioPercent(deal({ proteinPerServingGrams: 0, servingSizeGrams: 30 }))).toBeNull();
  });
});

describe('proteinReferenceCost', () => {
  it('sabit protein miktarının maliyetini hesaplar', () => {
    // 900 g / 30 g = 30 porsiyon, porsiyon başına 24 g → 720 g toplam protein.
    // 1000 TL / 720 g × 30 g = 41,67 TL
    const cost = proteinReferenceCost(deal({ currentPrice: 1000, size: '900 Gr', servingSizeGrams: 30, proteinPerServingGrams: 24 }));
    expect(cost).toBeCloseTo(41.67, 1);
  });

  it('daha yüksek protein oranı daha ucuz referans maliyet verir', () => {
    // Aynı fiyat ve paket, farklı protein yoğunluğu.
    const yogun = proteinReferenceCost(deal({ proteinPerServingGrams: 24 }))!;
    const seyrek = proteinReferenceCost(deal({ proteinPerServingGrams: 16 }))!;
    expect(yogun).toBeLessThan(seyrek);
  });

  it('protein verisi yoksa null döner', () => {
    expect(proteinReferenceCost(deal({ proteinPerServingGrams: null }))).toBeNull();
    expect(proteinReferenceCost(deal({ size: null, servingsPerPackage: null }))).toBeNull();
  });

  // Referans değişirse iki sayfa birbirinden ayrışır; bu test onu kilitliyor.
  it('referans miktar 30 gram', () => {
    expect(PROTEIN_REFERENCE_GRAMS).toBe(30);
  });
});

describe('pricePerServing', () => {
  it('porsiyon maliyetini hesaplar', () => {
    expect(pricePerServing(deal({ currentPrice: 900, size: '900 Gr', servingSizeGrams: 30 }))).toBe(30);
  });

  it('paketten tek porsiyon bile çıkmıyorsa null döner', () => {
    // Tutarsız veri: porsiyon paketten büyük.
    expect(pricePerServing(deal({ size: '20 Gr', servingSizeGrams: 30 }))).toBeNull();
  });
});

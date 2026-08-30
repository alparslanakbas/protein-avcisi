import { Deal } from './deal.model';
import { buildProductFacts } from './product-facts';

// Stok rozetindeki asıl incelik null ile false ayrımı: sekiz kaynaktan
// yalnızca üçü stok bilgisi veriyor, diğerlerinde alan null geliyor.
// Truthy kontrolü kullanılsaydı o beş markanın TÜM ürünlerinde "tükendi"
// yazardı — uydurma veri.
function deal(ustuneYaz: Partial<Deal> = {}): Deal {
  return {
    productId: 1,
    productName: 'Test Whey 1000g',
    productUrl: 'https://example.com/urun',
    imageUrl: null,
    category: 'protein-tozu',
    size: '1000 g',
    flavor: null,
    inStock: null,
    servingSizeGrams: null,
    servingsPerPackage: null,
    description: null,
    nutritionJson: null,
    proteinPerServingGrams: null,
    brandName: 'TestMarka',
    currentPrice: 100,
    referencePrice: 120,
    discountPercent: 16.7,
    storeOldPrice: null,
    storeDiscountPercent: null,
    scrapedAt: new Date().toISOString(),
    isAtThirtyDayLow: false,
    ...ustuneYaz,
  } as Deal;
}

function stokSatiri(d: Deal) {
  return buildProductFacts(d).find((f) => f.label === 'Stok durumu');
}

describe('buildProductFacts — stok durumu', () => {
  it('REGRESYON: stok bilgisi vermeyen kaynakta (null) satır GÖSTERMEZ', () => {
    expect(stokSatiri(deal({ inStock: null }))).toBeUndefined();
  });

  it('stokta olan üründe satır göstermez', () => {
    expect(stokSatiri(deal({ inStock: true }))).toBeUndefined();
  });

  it('stokta olmayan üründe satırı marka adıyla gösterir', () => {
    const satir = stokSatiri(deal({ inStock: false, brandName: 'HIQ' }));
    expect(satir).toBeDefined();
    expect(satir!.value).toContain('HIQ');
    // Ürünün takip edilmeye devam ettiği söylenmeli: kullanıcı kaydını
    // kaybettiğini sanmamalı.
    expect(satir!.value).toContain('izlemeye devam');
  });
});

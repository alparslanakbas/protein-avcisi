import { Deal } from './deal.model';
import { CATEGORY_LABELS } from './category-labels';
import {
  PROTEIN_REFERENCE_GRAMS,
  pricePerServing,
  proteinRatioPercent,
  proteinReferenceCost,
  servingsInPackage,
} from './value-metrics';

/**
 * Ürün sayfalarında gösterilen "bizim ölçtüğümüz" bilgi listesi.
 *
 * Neden var: daha önce bu alanda markanın kendi sitesinden çektiğimiz
 * açıklama metni birebir yayınlanıyordu. Metni ÇEKMEYE devam ediyoruz
 * (porsiyon ve besin değeri çıkarımı ona dayanıyor) ama artık
 * göstermiyoruz — başkasının pazarlama metnini olduğu gibi yeniden
 * yayınlamak hem bize ait olmayan bir içerik hem de arama motorlarının
 * "kopyalanmış içerik" saydığı bir kalıp.
 *
 * Yerine geçen bu liste tamamen kendi verimizden türüyor: fiyat geçmişi,
 * paket başına servis, servis maliyeti, protein yoğunluğu. Hiçbiri
 * tahmin değil — bir alan hesaplanamıyorsa satır hiç üretilmiyor,
 * uydurma bir değer yazılmıyor.
 */
export interface ProductFact {
  label: string;
  value: string;
}

const priceFormatter = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const ratingFormatter = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 2,
});

function formatPrice(value: number): string {
  return `${priceFormatter.format(value)} ₺`;
}

/**
 * @param discountEventCount Seçili dönemde ölçülen gerçek fiyat düşüşü
 *   sayısı. Fiyat geçmişi elde olmayan çağrılarda (kart, liste) verilmez.
 */
export function buildProductFacts(deal: Deal, discountEventCount?: number): ProductFact[] {
  const facts: ProductFact[] = [];

  if (deal.category) {
    facts.push({
      label: 'Kategori',
      value: CATEGORY_LABELS[deal.category] ?? deal.category,
    });
  }

  if (deal.size) {
    facts.push({ label: 'Paket', value: deal.size });
  }

  const servings = servingsInPackage(deal);
  if (servings !== null) {
    const source =
      deal.servingsPerPackage !== null
        ? `${deal.brandName} beyanı`
        : `${deal.size} ÷ ${deal.servingSizeGrams} g porsiyon`;
    // Paket ağırlığı ÷ porsiyon nadiren tam sayı çıkıyor (2 Kg ÷ 30 g gibi);
    // ondalıklı ham değeri basmak yerine yuvarlayıp "yaklaşık" diyoruz.
    const isExact = Number.isInteger(servings);
    facts.push({
      label: 'Paketten çıkan servis',
      value: `${isExact ? '' : 'yaklaşık '}${Math.round(servings)} servis (${source})`,
    });
  }

  if (deal.servingSizeGrams !== null) {
    facts.push({ label: 'Porsiyon', value: `${deal.servingSizeGrams} g` });
  }

  const perServing = pricePerServing(deal);
  if (perServing !== null) {
    facts.push({ label: 'Servis başına maliyet', value: formatPrice(perServing) });
  }

  if (deal.proteinPerServingGrams !== null) {
    facts.push({
      label: 'Porsiyon başına protein',
      value: `${deal.proteinPerServingGrams} g`,
    });
  }

  const ratio = proteinRatioPercent(deal);
  if (ratio !== null) {
    facts.push({
      label: 'Protein yoğunluğu',
      value: `%${ratio} (porsiyonun ne kadarı protein)`,
    });
  }

  const referenceCost = proteinReferenceCost(deal);
  if (referenceCost !== null) {
    facts.push({
      label: `${PROTEIN_REFERENCE_GRAMS} g protein maliyeti`,
      value: formatPrice(referenceCost),
    });
  }

  facts.push({
    label: 'Bizim ölçtüğümüz referans fiyat',
    value:
      deal.referencePrice > deal.currentPrice
        ? `${formatPrice(deal.referencePrice)} — güncel fiyat bunun %${deal.discountPercent} altında`
        : `${formatPrice(deal.referencePrice)} — güncel fiyat referansla aynı seviyede`,
  });

  if (deal.isAtThirtyDayLow) {
    facts.push({
      label: '30 günlük seyir',
      value: 'Güncel fiyat, son 30 günde ölçtüğümüz en düşük seviyede',
    });
  }

  if (discountEventCount !== undefined && discountEventCount > 0) {
    facts.push({
      label: 'Ölçülen fiyat düşüşü',
      value: `${discountEventCount} kez`,
    });
  }

  // Markanın kendi sitesindeki müşteri puanı. Bizim ölçümümüz değil, bu
  // yüzden etiketi markanın adıyla başlıyor — hemen aşağıdaki mağaza
  // indirimi satırıyla aynı "markanın beyanı" grubunda.
  if (deal.ratingValue !== null && deal.ratingCount !== null) {
    facts.push({
      label: `${deal.brandName} sitesindeki müşteri puanı`,
      value: `5 üzerinden ${ratingFormatter.format(deal.ratingValue)} (${deal.ratingCount} değerlendirme)`,
    });
  }

  if (deal.storeOldPrice !== null && deal.storeDiscountPercent !== null) {
    facts.push({
      label: `${deal.brandName} kendi sitesinde ne diyor`,
      value: `Eski fiyat ${formatPrice(deal.storeOldPrice)}, %${deal.storeDiscountPercent} indirim (markanın beyanı, bizim doğrulamamız değil)`,
    });
  }

  if (deal.flavor) {
    facts.push({ label: 'Aroma', value: deal.flavor });
  }

  // Yalnızca stok bilgisi VEREN kaynaklarda gösteriliyor. `=== false`
  // kontrolü zorunlu: null "bu marka stok bilgisi vermiyor" demek, "stokta
  // yok" değil. Sekiz kaynaktan üçü bu bilgiyi veriyor.
  //
  // Stokta olmayan ürün listeden çıkarılmıyor — fiyat geçmişi kesintisiz
  // kalsın diye taranmaya devam ediyor — ama kullanıcı boşuna mağazaya
  // gitmesin diye burada da açıkça söyleniyor.
  if (deal.inStock === false) {
    facts.push({
      label: 'Stok durumu',
      value: `Son kontrolümüzde ${deal.brandName} sitesinde tükenmişti. Fiyatını izlemeye devam ediyoruz.`,
    });
  }

  return facts;
}

/**
 * Ürün sayfasının schema.org `description` alanı.
 *
 * Google Search Console "description alanı eksik" uyarısı veriyordu. Metin
 * markanın tanıtım yazısından DEĞİL, kendi ölçümlerimizden kuruluyor —
 * başkasının pazarlama metnini yeniden yayınlamama kararıyla tutarlı
 * (bkz. yukarıdaki not). Her üründe farklı çıkıyor çünkü sayılar farklı.
 */
export function buildProductJsonLdDescription(deal: Deal): string {
  const parts: string[] = [];

  const category = deal.category ? (CATEGORY_LABELS[deal.category] ?? deal.category) : null;
  parts.push(
    category
      ? `${deal.brandName} markasının ${category} kategorisindeki ürünü.`
      : `${deal.brandName} markasının ürünü.`,
  );

  if (deal.size) parts.push(`Paket: ${deal.size}.`);
  if (deal.servingSizeGrams !== null) parts.push(`Porsiyon: ${deal.servingSizeGrams} g.`);
  if (deal.proteinPerServingGrams !== null) {
    parts.push(`Porsiyon başına ${deal.proteinPerServingGrams} g protein.`);
  }

  const perServing = pricePerServing(deal);
  if (perServing !== null) parts.push(`Servis başına ${formatPrice(perServing)}.`);

  parts.push('Fiyat geçmişi ProteinAvcısı tarafından düzenli olarak ölçülüyor.');

  return parts.join(' ');
}

import { Deal } from './deal.model';

// "Bu indirimde — peki bu ürün gerçekten iyi mi?" sorusunun cevabı.
//
// 28 Ağustos 2026'da beş yapay zekâ modeline aynı karşılaştırma soruldu; üçü
// birbirinden bağımsız olarak AYNI boşluğu işaret etti: servis başına fiyat
// gösteriyoruz ama ödenen paranın ne kadarının etken maddeye gittiğini
// göstermiyoruz. ChatGPT'nin somut ifadesiyle: *"gerçek besin değerlerinden
// 'servis maliyeti' ve '25 gram gerçek protein maliyeti' hesaplamak"*.
//
// Gerekli veri zaten toplanıyor (markanın besin değeri tablosundan gelen
// porsiyon ve porsiyon başına protein) — eksik olan yalnızca sunumdu.
//
// İLKE: veri yoksa null. Rakiplerden biri porsiyonu bilmediğinde standart
// 30 gram varsayıyor; biz varsaymıyoruz, alanı hiç göstermiyoruz.

// Karşılaştırmanın yapıldığı sabit protein miktarı. Paket boyutları ve
// porsiyonlar markadan markaya değiştiği için, ürünleri ancak ortak bir
// birim üzerinden yan yana koymak anlamlı oluyor.
//
// 30 g seçildi çünkü ürün incelemesi sayfasında bu ölçü zaten yayında
// kullanılıyordu; iki sayfada iki farklı referans göstermek okuyucuyu
// yanıltırdı. (Bir modelin önerisi 25 g'dı ama o, rakibin porsiyon
// varsaymasına verilen bir örnekti, referans dayatması değil.)
export const PROTEIN_REFERENCE_GRAMS = 30;

/** Paket etiketinden gram cinsinden ağırlık ("900 Gr" → 900). */
function parsePackageGrams(size: string | null): number | null {
  if (!size) return null;
  const match = /^(\d+(?:[.,]\d+)?)\s*Gr$/i.exec(size.trim());
  if (!match) return null;
  const grams = Number(match[1].replace(',', '.'));
  return grams > 0 ? grams : null;
}

/**
 * Paketten kaç porsiyon çıktığı. Öncelik sırası backend'deki
 * `CalculateServings` ile aynı: önce markanın doğrudan beyanı, o yoksa
 * paket ağırlığı ÷ porsiyon.
 */
export function servingsInPackage(deal: Deal): number | null {
  if (deal.servingsPerPackage && deal.servingsPerPackage > 0) return deal.servingsPerPackage;

  const packageGrams = parsePackageGrams(deal.size);
  if (packageGrams && deal.servingSizeGrams && deal.servingSizeGrams > 0) {
    return packageGrams / deal.servingSizeGrams;
  }
  return null;
}

/** Bir porsiyonun maliyeti. */
export function pricePerServing(deal: Deal): number | null {
  const servings = servingsInPackage(deal);
  if (!servings || servings < 1) return null;
  return deal.currentPrice / servings;
}

/**
 * Porsiyonun yüzde kaçı protein — "ödediğin paranın ne kadarı etken maddeye
 * gidiyor" sorusunun doğrudan cevabı. Bir üründe 30 gramlık porsiyonda 24 g
 * protein varsa %80; başka birinde 20 g varsa %66.
 */
export function proteinRatioPercent(deal: Deal): number | null {
  const { proteinPerServingGrams: protein, servingSizeGrams: serving } = deal;
  if (!protein || !serving || serving <= 0) return null;

  const ratio = (protein / serving) * 100;
  // Etiket verisi tutarsızsa (porsiyondan fazla protein) göstermiyoruz.
  if (ratio <= 0 || ratio > 100) return null;
  return Math.round(ratio);
}

/**
 * Sabit miktarda (25 g) proteinin maliyeti. Paket boyutundan ve porsiyon
 * farklarından arındırılmış olduğu için iki ürünü doğrudan karşılaştırmanın
 * en dürüst yolu bu.
 */
export function proteinReferenceCost(deal: Deal): number | null {
  const servings = servingsInPackage(deal);
  const protein = deal.proteinPerServingGrams;
  if (!servings || servings < 1 || !protein || protein <= 0) return null;

  const totalProtein = servings * protein;
  if (totalProtein <= 0) return null;

  return (deal.currentPrice / totalProtein) * PROTEIN_REFERENCE_GRAMS;
}

import { Deal } from './deal.model';
import { displayName } from './display-name';
import {
  PROTEIN_REFERENCE_GRAMS,
  pricePerServing,
  proteinRatioPercent,
  proteinReferenceCost,
  servingsInPackage,
} from './value-metrics';

/**
 * Ürün sayfasının kendi ölçümlerimizden üretilen anlatı bölümü.
 *
 * NEDEN VAR: Search Console, taradığı ürün sayfalarının bir kısmını dizine
 * eklemiyordu ("Tarandı - şu anda dizine eklenmiş değil"). Ölçtük: ürün
 * sayfaları birbirine %47,7 benziyor ve ~300 kelimeydi. Sebebi, markanın
 * tanıtım metnini (kopya içerik olduğu için) kaldırdıktan sonra geriye
 * yalnızca şablon bir etiket-değer listesi kalmasıydı — sayfalarda değişen
 * tek şey sayılardı.
 *
 * Buradaki cümleler yalnızca sayıları değil, YAPILARI da veriye göre
 * değiştiriyor: fiyatı hiç oynamamış bir ürünle 30 günün dibindeki bir ürün
 * tamamen farklı cümleler alıyor. Böylece her sayfa gerçekten farklılaşıyor.
 *
 * Tamamı kendi ölçümümüz — markadan kopyalanan tek kelime yok, ve
 * hesaplanamayan hiçbir şey için cümle kurulmuyor (uydurma yok).
 */

const priceFormatter = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const ratingFormatter = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 2,
});

function price(value: number): string {
  return `${priceFormatter.format(value)} ₺`;
}

/** Fiyat hikâyesi — ürünün geçmişine göre tamamen farklı cümleler. */
function priceParagraph(deal: Deal, discountEventCount?: number): string {
  const name = displayName(deal.productName);
  const sentences: string[] = [];

  if (deal.isAtThirtyDayLow) {
    sentences.push(
      `${name}, şu anda ${price(deal.currentPrice)} ile son 30 günde ölçtüğümüz en düşük seviyede.`,
    );
    sentences.push(
      `Aynı dönemde ${price(deal.referencePrice)} seviyesini de gördük; bugünkü fiyat bunun %${deal.discountPercent} altında.`,
    );
  } else if (deal.discountPercent > 0) {
    sentences.push(
      `${name} şu anda ${price(deal.currentPrice)}. Son 30 günde ölçtüğümüz en yüksek fiyat ${price(deal.referencePrice)} olduğu için bu, referansın %${deal.discountPercent} altında bir seviye.`,
    );
  } else {
    sentences.push(
      `${name} şu anda ${price(deal.currentPrice)} ve bu, son 30 günde ölçtüğümüz referans fiyatla aynı seviyede — yani şu an bir düşüş yok.`,
    );
  }

  if (discountEventCount !== undefined && discountEventCount > 0) {
    sentences.push(
      discountEventCount === 1
        ? 'Takip ettiğimiz dönemde fiyatı bir kez düştü.'
        : `Takip ettiğimiz dönemde fiyatı ${discountEventCount} kez düştü.`,
    );
  }

  // Markanın kendi "eski fiyat" etiketi bizim ölçümümüzden ayrı tutuluyor:
  // sitenin bütün değer önerisi bu ayrımın üstüne kurulu.
  if (deal.storeOldPrice !== null && deal.storeDiscountPercent !== null) {
    sentences.push(
      `${deal.brandName} kendi sitesinde eski fiyatı ${price(deal.storeOldPrice)} olarak gösteriyor (%${deal.storeDiscountPercent} indirim); bu markanın beyanı, bizim doğruladığımız düşüş yukarıdaki referansa dayanıyor.`,
    );
  }

  return sentences.join(' ');
}

/** Paket ekonomisi — yalnızca gerçek porsiyon verisi varsa. */
function economicsParagraph(deal: Deal): string | null {
  const servings = servingsInPackage(deal);
  const perServing = pricePerServing(deal);
  if (servings === null || perServing === null) return null;

  const sentences: string[] = [];
  const source =
    deal.servingsPerPackage !== null
      ? `${deal.brandName} paketten ${Math.round(servings)} servis çıktığını belirtiyor`
      : `${deal.size} paket ve ${deal.servingSizeGrams} g porsiyona göre paketten yaklaşık ${Math.round(servings)} servis çıkıyor`;
  sentences.push(`${source}, yani servis başına ${price(perServing)} düşüyor.`);

  const ratio = proteinRatioPercent(deal);
  const refCost = proteinReferenceCost(deal);
  if (deal.proteinPerServingGrams !== null && ratio !== null) {
    sentences.push(
      `Porsiyon başına ${deal.proteinPerServingGrams} g protein var; bu, porsiyonun %${ratio}'inin protein olduğu anlamına geliyor.`,
    );
    if (refCost !== null) {
      sentences.push(
        `${PROTEIN_REFERENCE_GRAMS} g proteine ulaşmanın maliyeti bu üründe ${price(refCost)}.`,
      );
    }
  }

  return sentences.join(' ');
}

/** Markanın kendi müşteri puanı — bizim değerlendirmemiz olmadığı açıkça yazılı. */
function ratingParagraph(deal: Deal): string | null {
  if (deal.ratingValue === null || deal.ratingCount === null) return null;
  // Uzun bir sorumluluk cümlesi her üründe birebir tekrarlanıyordu ve
  // sayfaları birbirine benzetiyordu; açıklamanın tamamı zaten
  // /nasil-calisiyoruz sayfasında. Burada yalnızca kaynağı belirtiyoruz.
  return (
    `${deal.brandName} kendi sitesinde bu ürüne ${deal.ratingCount} değerlendirme ile ` +
    `5 üzerinden ${ratingFormatter.format(deal.ratingValue)} ortalama veriyor (markanın müşteri puanı, bizim değil).`
  );
}

/** Neyi ölçemediğimizi de söylüyoruz — eksik veriyi sessizce gizlemiyoruz. */
function limitationsParagraph(deal: Deal): string | null {
  const missing: string[] = [];
  if (deal.servingSizeGrams === null && deal.servingsPerPackage === null) {
    missing.push('porsiyon büyüklüğü');
  }
  if (!deal.nutritionJson) missing.push('besin değeri tablosu');
  if (missing.length === 0) return null;

  return (
    `${deal.brandName}, bu ürün için ${missing.join(' ve ')} paylaşmıyor. ` +
    'Bu yüzden ilgili hesaplamaları burada göremiyorsun — tahmini bir değer üretmek yerine ' +
    'alanı boş bırakmayı tercih ediyoruz.'
  );
}

/**
 * Besin değeri tablosundan üretilen cümle. Ürünler arasında en çok değişen
 * veri bu (her ürünün tablosu farklı satırlar taşıyor), bu yüzden sayfayı
 * ayırt etmekte en güçlü katkıyı sağlıyor.
 */
function nutritionParagraph(deal: Deal): string | null {
  if (!deal.nutritionJson) return null;

  let rows: Record<string, string>;
  try {
    rows = JSON.parse(deal.nutritionJson) as Record<string, string>;
  } catch {
    return null;
  }

  const entries = Object.entries(rows).filter(([k, v]) => k && v);
  if (entries.length === 0) return null;

  const listed = entries.slice(0, 6).map(([k, v]) => `${k.toLowerCase()} ${v}`);
  const portion = deal.servingSizeGrams !== null ? `${deal.servingSizeGrams} g porsiyonda` : 'Porsiyon başına';
  const extra = entries.length > 6 ? ` Tabloda toplam ${entries.length} satır var.` : '';

  return `${deal.brandName} etiketine göre ${portion} ${listed.join(', ')} bulunuyor.${extra}`;
}

export function buildProductNarrative(deal: Deal, discountEventCount?: number): string[] {
  const paragraphs: (string | null)[] = [
    priceParagraph(deal, discountEventCount),
    economicsParagraph(deal),
    nutritionParagraph(deal),
    ratingParagraph(deal),
    limitationsParagraph(deal),
    // Jenerik bir kapanış cümlesi bilinçli olarak YOK: her üründe birebir
    // aynı olduğu için sayfaları birbirine benzetmekten başka işe yaramıyordu.
  ];

  return paragraphs.filter((p): p is string => p !== null);
}

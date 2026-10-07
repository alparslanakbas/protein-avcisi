// /api/value-picks — "Hangi takviye?" sayfalarının ürün listesi. Kategoride
// kilogram fiyatına göre sıralı, her markadan bir ürün (bkz. backend
// ValuePickRanker).
export interface ValuePick {
  productId: number;
  productName: string;
  brandName: string;
  imageUrl: string | null;
  size: string | null;
  // Ürünü satan mağaza; null ise markanın kendi sitesi.
  seller: string | null;
  currentPrice: number;
  pricePerKg: number;
  referencePrice: number;
  // Bizim fiyat geçmişimize dayanan indirim; yoksa 0.
  discountPercent: number;
  isAtThirtyDayLow: boolean;
  // Takip başladığından beri en düşük fiyattaysa takibin başladığı an (bkz. Deal.lowestSince).
  lowestSince?: string | null;
  inStock: boolean | null;
  storeUrl: string;
}

export interface ValuePicks {
  items: ValuePick[];
  // Korumalardan geçen, kilogram fiyatı hesaplanabilen ürün sayısı.
  eligibleCount: number;
}

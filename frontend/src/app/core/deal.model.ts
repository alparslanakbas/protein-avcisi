export interface Deal {
  productId: number;
  productName: string;
  productUrl: string;
  imageUrl: string | null;
  category: string | null;
  size: string | null;
  flavor: string | null;
  servingSizeGrams: number | null;
  // Paketten kaç servis çıktığı — markanın doğrudan beyanı (şimdilik
  // yalnızca ProteinOcean; o markada paket gramajı hiç gelmiyor).
  servingsPerPackage: number | null;
  // Markanın kendi sitesinden gelen gerçek ürün açıklaması — sadece marka
  // bunu sağlıyorsa dolu (şimdilik HIQ), yoksa null.
  description: string | null;
  brandName: string;
  currentPrice: number;
  referencePrice: number;
  discountPercent: number;
  storeOldPrice: number | null;
  storeDiscountPercent: number | null;
  scrapedAt: string;
  isAtThirtyDayLow: boolean;
}

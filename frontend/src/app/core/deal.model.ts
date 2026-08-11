export interface Deal {
  productId: number;
  productName: string;
  productUrl: string;
  imageUrl: string | null;
  category: string | null;
  size: string | null;
  flavor: string | null;
  brandName: string;
  currentPrice: number;
  referencePrice: number;
  discountPercent: number;
  scrapedAt: string;
}

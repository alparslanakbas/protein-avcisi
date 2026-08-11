export interface PricePoint {
  price: number;
  scrapedAt: string;
}

export interface PriceHistory {
  productId: number;
  productName: string;
  brandName: string;
  points: PricePoint[];
  currentPrice: number;
  minPrice: number;
  maxPrice: number;
}

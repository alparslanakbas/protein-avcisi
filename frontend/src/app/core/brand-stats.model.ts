export interface BrandStats {
  totalProducts: number;
  discountCount: number;
  thirtyDayLowCount: number;
  averageDiscountPercent: number | null;
  lastScanAt: string | null;
}

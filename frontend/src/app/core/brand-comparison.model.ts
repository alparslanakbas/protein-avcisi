import { BrandStats } from './brand-stats.model';

/** Bir markanın bir kategorideki kilogram başına en uygun ürünü. */
export interface ComparisonPick {
  productId: number;
  productName: string;
  size: string | null;
  price: number;
  pricePerKg: number;
}

export interface CategoryComparison {
  category: string;
  /** Ham ortalama: paket boyutuna bakmaz, yalnız kilogram fiyatı olmayan kategorilerde gösterilir. */
  brand1AvgPrice: number | null;
  brand1Count: number;
  brand2AvgPrice: number | null;
  brand2Count: number;
  /** Kilogram fiyatı medyanı (gramajı bilinen, korumalardan geçen stoktaki ürünler). */
  brand1MedianPerKg: number | null;
  brand1KgCount: number;
  brand1BestPick: ComparisonPick | null;
  brand2MedianPerKg: number | null;
  brand2KgCount: number;
  brand2BestPick: ComparisonPick | null;
}

/** Son 30 günde ardışık taramalar arasındaki fiyat değişimleri. */
export interface PriceActivity {
  drops: number;
  rises: number;
  changedProducts: number;
}

export interface BrandComparison {
  brand1: string;
  brand2: string;
  brand1TotalProducts: number;
  brand2TotalProducts: number;
  categories: CategoryComparison[];
  brand1Activity: PriceActivity | null;
  brand2Activity: PriceActivity | null;
  brand1Stats: BrandStats | null;
  brand2Stats: BrandStats | null;
}

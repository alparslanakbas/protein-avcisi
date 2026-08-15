export interface CategoryComparison {
  category: string;
  brand1AvgPrice: number | null;
  brand1Count: number;
  brand2AvgPrice: number | null;
  brand2Count: number;
}

export interface BrandComparison {
  brand1: string;
  brand2: string;
  brand1TotalProducts: number;
  brand2TotalProducts: number;
  categories: CategoryComparison[];
}

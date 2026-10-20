import { ComparisonPick, PriceActivity } from './brand-comparison.model';

/** Markanın bir toz kategoride kilogram fiyatı konumu (sitedeki bütün markalara göre). */
export interface BrandKgPosition {
  category: string;
  brandMedianPerKg: number | null;
  brandKgCount: number;
  marketMedianPerKg: number | null;
  marketKgCount: number;
  marketBrandCount: number;
  bestPick: ComparisonPick;
}

export interface BrandInsights {
  brand: string;
  activity: PriceActivity;
  categories: BrandKgPosition[];
}

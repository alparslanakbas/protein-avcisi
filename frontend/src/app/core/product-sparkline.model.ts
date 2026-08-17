import { PricePoint } from './price-history.model';

export interface ProductSparkline {
  productId: number;
  points: PricePoint[];
}

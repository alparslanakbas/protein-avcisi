export interface Coupon {
  id: number;
  brandName: string;
  code: string;
  description: string;
  validUntil: string | null;
  lastVerifiedAt: string;
}

export interface Coupon {
  id: number;
  brandName: string | null;
  seller: string | null;
  code: string | null;
  description: string;
  validUntil: string | null;
  lastVerifiedAt: string;
}

export interface BrandStats {
  totalProducts: number;
  discountCount: number;
  thirtyDayLowCount: number;
  averageDiscountPercent: number | null;
  lastScanAt: string | null;
  // Kapsamdaki ürünlerin ortalama güncel fiyatı — marka × kategori
  // sayfasında kategorinin geneliyle karşılaştırmak için.
  averagePrice: number | null;
}

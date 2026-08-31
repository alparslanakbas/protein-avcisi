export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FilterOptions {
  brands: string[];
  categories: string[];
  // Ürünün satın alındığı yer — marka (üretici) ile aynı şey değil.
  // Bayi ürünü yoksa boş gelir ve arayüz satıcı kutusunu hiç göstermez.
  // İlk eleman "Markanın kendi sitesi" etiketi (backend'de Seller = NULL).
  sellers: string[];
}

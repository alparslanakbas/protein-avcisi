/**
 * Marka adını adres parçasına çevirir.
 *
 * Marka adları boşluk içerebiliyor ("Torq Nutrition"); ad doğrudan adrese
 * konulunca sitemap'e `%20` taşıyan adresler giriyordu. Boşluk yerine tire
 * kullanmak hem alışılmış hem de okunur bir adres veriyor.
 *
 * Türkçe karakterler BİLİNÇLİ olarak korunuyor: mevcut markaların hiçbirinde
 * yok, ama olsaydı slugify etmek eski adresleri bozardı. Gerekirse ayrıca
 * ele alınır.
 */
export function brandSlug(brandName: string): string {
  return brandName.trim().toLowerCase().replace(/\s+/g, '-');
}

/**
 * Adres parçasından gerçek marka adını bulur.
 *
 * Geriye dönük uyumluluk: tire yerine boşluk taşıyan eski adresler
 * (`torq nutrition-vs-...`) hâlâ çözülüyor — o adresler bir süre sitemap'te
 * yer aldı, kırılmamalılar.
 */
export function resolveBrandFromSlug(slug: string, brands: readonly string[]): string | null {
  const normalized = brandSlug(slug);
  return brands.find((b) => brandSlug(b) === normalized) ?? null;
}

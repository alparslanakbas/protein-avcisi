import { canonicalOrigin } from './canonical-link';

export interface BreadcrumbItem {
  name: string;
  path: string;
}

// schema.org BreadcrumbList — kategori/marka/ürün/rehber sayfalarında
// ortak kullanılıyor. Google arama sonucunda çıplak URL yerine
// "Ana Sayfa › Kategori › Ürün" gösterebiliyor (rakip analizinde eksik
// olduğumuz, göze çarpan bir sinyaldi).
export function buildBreadcrumbJsonLd(document: Document, items: BreadcrumbItem[]): object {
  const origin = canonicalOrigin(document);
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: items.map((item, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: item.name,
      item: `${origin}${item.path}`,
    })),
  };
}

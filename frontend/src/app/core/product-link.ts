import { slugify } from './slugify';

export interface ProductLinkSource {
  productId: number;
  productName: string;
}

// Ürün detay sayfasının kanonik yolu — sitemap ve canonical etiketiyle aynı
// biçim (bkz. deals-list.ts, server.ts).
export function productPath(deal: ProductLinkSource): string {
  return `/urun/${deal.productId}/${slugify(deal.productName)}`;
}

// Ürün kartları gerçek <a href> olmak ZORUNDA. Arama motorları yalnızca gerçek
// bağlantıları takip eder ve site içi otorite yalnızca onlar üzerinden akar;
// kartlar <button> + JS tıklaması olduğu sürece ürün sayfaları site içi bağlantı
// grafiğinde izole kalıyor, yalnızca sitemap üzerinden keşfediliyordu (27 Ağustos
// ölçümü: ana sayfada 52 iç bağlantı, ürüne giden 0).
//
// Ama tıklama davranışı (modalı açmak) korunmalı. Bu yardımcı, olayın uygulama
// içinde mi ele alınacağını yoksa tarayıcıya mı bırakılacağını söylüyor:
// modifier'lı tık ve orta tık tarayıcıya kalır ki "yeni sekmede aç" çalışsın —
// kartlar buton olduğu sürece bu da kırıktı.
export function shouldHandleInApp(event: MouseEvent): boolean {
  return event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

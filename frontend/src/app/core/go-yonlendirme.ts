import { productPath } from './product-link';

/**
 * Eski "Mağazaya git" adresi /go/{id}'nin KALICI hedefi: ürünün kanonik sayfası.
 *
 * Bağlantılar Eylül başından beri doğrudan mağazaya gidiyor (sayaç beacon'la); /go/ yalnız eski
 * e-postalar ve dizinde kalan adresler için 302 + noindex ile duruyordu. 27 Kasım ölçümü: 36 günde
 * /go/'a gelen 19.470 isteğin hepsi bottu, Googlebot'un www isteklerinin %46,5'i bu adreslere
 * gidiyordu (576 eski adres, her biri haftada ~7 kez). 302 "geçici" dediği için Google yoklamayı
 * bırakmıyordu; 301 ile adres bizim ürün sayfamıza katlanıyor. Disallow bilerek kullanılmadı
 * (bkz. server.ts robots.txt ucu).
 *
 * Ürün adı okunamazsa slug'sız ürün adresi: sayfa kendisi kanonik adrese (301) ya da 404'e
 * götürüyor. Kimlik sayı değilse null (istek 404'e düşer).
 */
export function goHedefi(id: string, urunAdi: string | null): string | null {
  if (!/^\d+$/.test(id)) return null;
  const kimlik = Number(id);
  const yol = urunAdi ? productPath({ productId: kimlik, productName: urunAdi }) : '';
  return yol && !yol.endsWith('/') ? yol : `/urun/${kimlik}`;
}

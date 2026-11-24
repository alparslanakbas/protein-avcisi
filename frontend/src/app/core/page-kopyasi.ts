// Ürün ve inceleme sayfasının ?page= kopyasını temiz adrese çeviren 301'in hedefi.
//
// a9f6c8a'dan (27 Ekim) önce ürün rotasındaki fırsat listesinin sayfalayıcısı göreli bağlantı
// kullanıyordu ve /urun/{id}/{slug}?page=N kopyaları üretiyordu. Sayfalar artık bu bağlantıyı
// vermiyor ama Google düzeltmeden önce keşfettiği kopyaları hâlâ tarıyor. 24 Kasım ölçümü
// (Caddy günlüğü, Googlebot): düzeltmeden sonra her hafta 150-700 daha önce hiç istenmemiş kopya
// geldi, ürün taramalarındaki payları H46'da %26, H47'de %12 oldu. Canonical doğru olduğu için
// dizine girmiyorlar ama tarama bütçesini harcıyorlar; 301 kopyayı temiz adrese bağlıyor.
// Ürün sayfasında page parametresinin işlevi yok (sayfalayıcı /?page=N'e gidiyor); diğer
// parametreler (utm vb.) korunuyor.
const URUN_YOLU = /^\/urun(-inceleme)?\/\d+(\/[^/]+)?\/?$/;

export function pageKopyasiHedefi(yol: string, adres: string): string | null {
  if (!URUN_YOLU.test(yol)) return null;
  const url = new URL(adres, 'http://yerel');
  if (!url.searchParams.has('page')) return null;
  url.searchParams.delete('page');
  return url.pathname + url.search;
}

/**
 * Sayfaların okuduğu sorgu parametreleri (ana sayfa liste durumu, marka
 * sayfasının bayi görünümü, listelerdeki ürün penceresi). Bunların dışında
 * parametre taşıyan istek SSR önbelleğine hiç girmiyor.
 */
const BILINEN_PARAMETRELER = new Set([
  'page',
  'search',
  'brands',
  'categories',
  'sellers',
  'min',
  'max',
  'sort',
  'view',
  'satici',
  'urun',
]);

/**
 * SSR önbelleğinin anahtarı; `null` = bu istek önbelleğe girmez.
 *
 * NEDEN (güvenlik incelemesi, 26 Eylül): anahtar ham adresti, yani
 * `?r=<rastgele>` her istekte yeni bir girdi açıyor ve 120 girdilik önbelleği
 * çöple doldurup sıcak sayfaları dışarı itebiliyordu.
 *
 * İncelemenin önerisi "bilinmeyen parametreleri anahtardan at" idi; BİLEREK
 * yapılmadı. Sayfalama bağlantıları `queryParamsHandling="merge"` ile mevcut
 * sorgunun tamamını taşıyor: `/?r=kotu` isteğinin HTML'i `?r=kotu&page=2`
 * bağlantıları içerir ve temiz `/` anahtarına yazılsaydı sonraki her
 * ziyaretçiye (ve Googlebot'a) o bağlantılar gönderilirdi. Onun yerine
 * tanınmayan parametreli istek önbelleğe hiç girmiyor ve anahtar ham adres
 * olarak kalıyor. Liste eksik kalırsa sonuç yalnızca "önbelleğe girmedi" —
 * hiçbir zaman "yanlış sayfa verildi" değil.
 */
export function ssrOnbellekAnahtari(method: string, originalUrl: string): string | null {
  if (method !== 'GET') return null;
  const url = new URL(originalUrl, 'http://yerel');
  // Takip listesi tarayıcıdaki anahtara bağlı; kurtarma bağlantısı ise
  // (`recover`, listede yok) tek kişiye ait bir belirteç taşıyor.
  if (url.pathname.startsWith('/favorilerim')) return null;
  for (const ad of url.searchParams.keys()) {
    if (!BILINEN_PARAMETRELER.has(ad)) return null;
  }
  return originalUrl;
}

/**
 * Ana sayfadaki marka/kategori/satıcı filtre kutularının davranışı.
 *
 * Bu kutular İKİ İŞ birden yapıyor: bir filtre EKLEME düğmesi ve o boyutun
 * DURUM göstergesi. Seçilenler aşağıda silinebilir çipler olarak duruyor.
 *
 * Karar buraya çıkarıldı çünkü iki ayrı hata da tam olarak buradaydı ve
 * ikisini de kullanıcı bildirdi:
 *
 *   1. Kutu, filtre aktifken bile "Tüm markalar" yazıyordu — durum
 *      göstergesi gibi duran ama aslında placeholder olan bir kutu.
 *   2. "Tüm markalar"ı seçmek filtreyi temizlemiyordu. Kutunun değeri boş
 *      dizeyken boş seçeneği tekrar seçmek bir DEĞİŞİKLİK olmadığı için
 *      tarayıcı `change` olayını hiç tetiklemiyor; üstelik tetiklense bile
 *      eski handler boş değeri yok sayıyordu.
 */

const ACTIVE_PREFIX = '__aktif__';

/**
 * Kutuda gösterilecek değer. Filtre yoksa boş dize (placeholder seçeneği),
 * varsa durum seçeneği.
 *
 * Değer SEÇİM SAYISINI da taşıyor. Taşımasaydı ikinci bir marka eklendiğinde
 * bağlanan değer değişmez ('__aktif__' -> '__aktif__') ve Angular DOM'a geri
 * yazmazdı; kutu kullanıcının son tıkladığı markanın adında donup kalırdı.
 * Bu, daha önce `[value]=""` ile yaşanan hatanın birebir aynı sınıfı:
 * değişmeyen bir bağlama yeniden uygulanmaz.
 */
export function filterSelectValue(selectedCount: number): string {
  return selectedCount > 0 ? `${ACTIVE_PREFIX}:${selectedCount}` : '';
}

export type FilterSelection =
  /** Kullanıcı bir değer seçti; o filtre eklenecek (ya da zaten varsa çıkarılacak). */
  | { readonly kind: 'toggle'; readonly value: string }
  /** Kullanıcı "Tüm markalar/kategoriler/satıcılar"ı seçti; boyut temizlenecek. */
  | { readonly kind: 'clear' }
  /** Kutunun kendi durum seçeneği; kullanıcı bir şey değiştirmedi. */
  | { readonly kind: 'ignore' };

export function readFilterSelection(value: string): FilterSelection {
  if (value.startsWith(ACTIVE_PREFIX)) return { kind: 'ignore' };
  return value ? { kind: 'toggle', value } : { kind: 'clear' };
}

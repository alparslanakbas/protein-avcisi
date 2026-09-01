/**
 * "Mağazaya git" bağlantısının hangi bağlamda açılacağı.
 *
 * TARAYICIDA `_blank`: kullanıcı mağazaya bakarken sitemiz açık kalsın.
 * Ortaklık bağlantılarında da alışılmış davranış bu.
 *
 * KURULU PWA'DA `_self`. Sebebi ölçüldü: `_blank` sıfırdan bir tarama
 * bağlamı açıyor ve o bağlamın geçmişinde yalnızca yönlendirme zinciri
 * bulunuyor (`/go/{id}` → 302 → mağaza). Geride dönülecek bir sayfa
 * olmadığı için geri tuşu bağlamı KAPATIYOR; tarayıcıda bu yalnızca bir
 * sekmenin kapanması demek ama kurulu uygulamada doğrudan telefonun ana
 * ekranına düşmek demek. Kullanıcı bunu bildirdi: "geri bastığımda
 * telefonun menüsüne atıyor".
 *
 * Aynı bağlamda gezinince geçmiş korunuyor: geri tuşu ürün sayfasına
 * dönüyor ve tarayıcı kaydırma konumunu da geri yüklüyor (test edildi).
 *
 * Sunucuda (SSR) `_blank` dönülüyor — orada `window` yok ve HTML zaten
 * tarayıcı için üretiliyor; kurulu uygulamada değer hidrasyondan sonra
 * düzeltiliyor.
 */
export type StoreLinkTarget = '_blank' | '_self';

export function storeLinkTarget(isBrowser: boolean): StoreLinkTarget {
  if (!isBrowser) return '_blank';

  // Android/Chrome ve masaüstü PWA'lar `display-mode: standalone`
  // bildiriyor; iOS'ta Safari `navigator.standalone` kullanıyor.
  const standalone =
    window.matchMedia?.('(display-mode: standalone)').matches === true ||
    window.matchMedia?.('(display-mode: fullscreen)').matches === true ||
    (navigator as unknown as { standalone?: boolean }).standalone === true;

  return standalone ? '_self' : '_blank';
}

// SPA gezinmesinde "sayfayı en üste al" kararı.
//
// Bu mantık app.ts'in içine gömülüydü ve test edilemiyordu; sonuç olarak bir
// hata (fragment'in yol karşılaştırmasına dahil edilmesi) üretime kadar gitti
// ve kullanıcı tarafından bulundu. Saf fonksiyon olarak ayrıldı.

/**
 * Karşılaştırma için yolu normalleştirir: sorgu ve fragment atılır.
 *
 * '#' KESİLMEK ZORUNDA. `Router.url` fragment'i içerir; kesilmezse sayfa içi
 * bir bölüm bağlantısına tıklamak (ör. /gizlilik-politikasi#kvkk-haklari)
 * "başka bir sayfaya geçildi" gibi görünür ve kaydırma sıfırlanarak
 * tarayıcının az önce yaptığı bölüme gitme işlemi geri alınır.
 */
export function routePath(url: string): string {
  return url.split(/[?#]/)[0];
}

export interface NavigationSnapshot {
  /** Rotanın yaprak component'i; aynı component içinde kalan gezinmeleri ayırt etmek için. */
  component: unknown;
  /** routePath() ile normalleştirilmiş yol. */
  path: string;
}

/**
 * Gezinme sonrası sayfa en üste alınmalı mı?
 *
 * `previous` null ise bu ilk gezinmedir ve sıfırlama YAPILMAZ: belge zaten
 * en üstte açılır, ayrıca adres bir fragment içeriyorsa (paylaşılmış bir
 * bölüm bağlantısı) ya da tarayıcı geri/ileri sonrası kaydırma konumunu
 * geri yüklediyse, onların yaptığını bozmamak gerekir.
 */
export function shouldResetScroll(previous: NavigationSnapshot | null, next: NavigationSnapshot): boolean {
  if (previous === null) return false;

  const changed = previous.component !== next.component || previous.path !== next.path;
  if (!changed) return false;

  // Tek istisna ürün modalı: ana sayfada modal açılıp kapanması '/' ile
  // '/urun/...' arasında gerçek bir yol değişimi gibi görünür ama aslında
  // aynı sayfanın üstündeki bir katmandır (bkz. DealsRouteReuseStrategy).
  // Burada sıfırlanırsa, sayfanın ortasındaki bir ürüne tıklayıp modalı
  // kapatan kişi kendini en başta bulur.
  //
  // İstisna yalnızca aynı component içinde kalırken geçerli: ürün
  // modalından marka sayfasına geçmek gerçek bir sayfa değişimidir.
  const sameComponent = previous.component === next.component;
  const productModalNav = sameComponent && (next.path.startsWith('/urun/') || previous.path.startsWith('/urun/'));

  return !productModalNav;
}

import { isPlatformBrowser } from '@angular/common';
import { Directive, ElementRef, PLATFORM_ID, inject } from '@angular/core';

import { storeLinkTarget } from './store-link';

/**
 * "Mağazaya git" bağlantılarını kurulu PWA'da aynı bağlamda açar.
 *
 * NEDEN: `target="_blank"` sıfırdan bir tarama bağlamı açıyor ve o bağlamın
 * geçmişinde yalnızca yönlendirme zinciri oluyor (`/go/{id}` → 302 → mağaza).
 * Geride dönülecek sayfa olmadığı için geri tuşu bağlamı KAPATIYOR.
 * Tarayıcıda bu yalnızca bir sekmenin kapanması; kurulu uygulamada ise
 * doğrudan telefonun ana ekranına düşmek demek. Kullanıcı bildirdi:
 * "geri bastığımda telefonun menüsüne atıyor".
 *
 * Ölçümle doğrulandı: yeni bağlamda `history.length` 2 (yalnızca yönlendirme
 * zinciri) ve `history.back()` sonrası sekme tamamen boşalıyor. Aynı bağlamda
 * gezinildiğinde ise geri tuşu ürün sayfasına dönüyor ve tarayıcı kaydırma
 * konumunu da geri yüklüyor.
 *
 * Seçici `rel`'e bakıyor: ortaklık bağlantılarının hepsinde "sponsored" var,
 * dolayısıyla şablonlarda hiçbir değişiklik gerekmiyor. Şablonlardaki
 * `target="_blank"` varsayılan olarak KALIYOR — SSR çıktısı ve JavaScript'siz
 * durum için doğru değer o; yönerge yalnızca kurulu uygulamada aşağı çekiyor.
 */
@Directive({
  selector: 'a[rel~="sponsored"]',
  standalone: true,
})
export class StoreLinkTargetDirective {
  constructor() {
    const platformId = inject(PLATFORM_ID);
    if (!isPlatformBrowser(platformId)) return;

    const hedef = storeLinkTarget(true);
    if (hedef === '_blank') return;

    inject<ElementRef<HTMLAnchorElement>>(ElementRef).nativeElement.target = hedef;
  }
}

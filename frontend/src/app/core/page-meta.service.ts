import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

import { canonicalOrigin, setCanonicalLink } from './canonical-link';

export interface PageMetaOptions {
  title: string;
  description: string;
  canonicalPath: string;
  ogType?: string;
  ogImage?: string;
  // Paylaşım kartında (WhatsApp/Twitter/Facebook) Google'daki <title>'dan
  // FARKLI bir metin göstermek istediğimizde (ör. ürün sayfasında paylaşımda
  // fiyat görünsün ama Google'a giden <title> fiyatsız kalsın diye) — yoksa
  // options.title kullanılır.
  ogTitle?: string;
  // Sayfa arama motoru dizinine girmemeli (kişiye özel içerik, ya da markanın
  // artık taramada döndürmediği bir ürün kaydı).
  //
  // Değer verilmediğinde servis etiketi KALDIRIYOR — bu şart: tek sayfa
  // uygulamasında bir sayfada eklenen robots etiketi, sonraki sayfaya
  // geçildiğinde geride kalsaydı normal sayfalar da dizinden düşerdi.
  noIndex?: boolean;
}

// 2026-08-15 kod kalitesi taraması: title/description/OG/canonical ayarlama
// mantığı 10 sayfada elle kopyalanmıştı — kopyalama sırasında 5 sayfada
// (rehber listesi, favorilerim, nasıl-çalışıyoruz, gizlilik/çerez politikası)
// og:title/og:description hiç eklenmemiş kalmıştı: biri bu sayfaları
// paylaştığında WhatsApp/Twitter kartında hâlâ ana sayfanın başlığı
// görünüyordu. og alanları burada zorunlu (options nesnesinin bir parçası)
// olduğu için bu sınıf hatası artık yapısal olarak tekrarlanamaz.
//
// 2026-08-23 SEO turu: og:url/og:locale/twitter:title/description/image
// hiç eklenmiyordu (dış bir kod incelemesinde bulundu, kodla doğrulandı) —
// eklendi. og:site_name index.html'de statik olarak zaten var ama her
// sayfada updateTag ile teyit etmek, ileride index.html'deki statik
// etiketin yanlışlıkla silinmesi/değişmesi ihtimaline karşı daha güvenli.
@Injectable({ providedIn: 'root' })
export class PageMetaService {
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  set(options: PageMetaOptions): void {
    const origin = canonicalOrigin(this.document);
    const ogImage = options.ogImage ?? `${origin}/og-image.png`;
    const ogTitle = options.ogTitle ?? options.title;

    this.titleService.setTitle(options.title);
    this.metaService.updateTag({ name: 'description', content: options.description });
    this.metaService.updateTag({ property: 'og:title', content: ogTitle });
    this.metaService.updateTag({ property: 'og:description', content: options.description });
    this.metaService.updateTag({ property: 'og:type', content: options.ogType ?? 'website' });
    this.metaService.updateTag({ property: 'og:image', content: ogImage });
    this.metaService.updateTag({ property: 'og:url', content: `${origin}${options.canonicalPath}` });
    this.metaService.updateTag({ property: 'og:locale', content: 'tr_TR' });
    this.metaService.updateTag({ property: 'og:site_name', content: 'Protein Avcısı' });
    this.metaService.updateTag({ name: 'twitter:title', content: ogTitle });
    this.metaService.updateTag({ name: 'twitter:description', content: options.description });
    this.metaService.updateTag({ name: 'twitter:image', content: ogImage });

    if (options.noIndex) {
      this.metaService.updateTag({ name: 'robots', content: 'noindex, follow' });
    } else {
      // Kaldırmak, eklemek kadar önemli — bkz. noIndex alanının açıklaması.
      this.metaService.removeTag("name='robots'");
    }

    setCanonicalLink(this.document, options.canonicalPath);
  }
}

// deals-list ve article-page'de "varsa güncelle, yoksa oluştur" JSON-LD
// script etiketi mantığı elle tekrarlanıyordu. Element referansını burada
// tutmuyoruz (deals-list gibi kullanıcılar ürün seçimine göre eklenip
// kaldırılması gerekebiliyor) — çağıran taraf referansı kendi tutup bir
// sonraki çağrıda geri veriyor.
export function upsertJsonLdScript(document: Document, existingEl: HTMLScriptElement | null, data: unknown): HTMLScriptElement {
  let el = existingEl;
  if (!el) {
    el = document.createElement('script');
    el.type = 'application/ld+json';
    document.head.appendChild(el);
  }
  el.textContent = JSON.stringify(data);
  return el;
}

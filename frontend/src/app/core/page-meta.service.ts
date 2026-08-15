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
}

// 2026-08-15 kod kalitesi taraması: title/description/OG/canonical ayarlama
// mantığı 10 sayfada elle kopyalanmıştı — kopyalama sırasında 5 sayfada
// (rehber listesi, favorilerim, nasıl-çalışıyoruz, gizlilik/çerez politikası)
// og:title/og:description hiç eklenmemiş kalmıştı: biri bu sayfaları
// paylaştığında WhatsApp/Twitter kartında hâlâ ana sayfanın başlığı
// görünüyordu. og alanları burada zorunlu (options nesnesinin bir parçası)
// olduğu için bu sınıf hatası artık yapısal olarak tekrarlanamaz.
@Injectable({ providedIn: 'root' })
export class PageMetaService {
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  set(options: PageMetaOptions): void {
    const ogImage = options.ogImage ?? `${canonicalOrigin(this.document)}/og-image.png`;

    this.titleService.setTitle(options.title);
    this.metaService.updateTag({ name: 'description', content: options.description });
    this.metaService.updateTag({ property: 'og:title', content: options.title });
    this.metaService.updateTag({ property: 'og:description', content: options.description });
    this.metaService.updateTag({ property: 'og:type', content: options.ogType ?? 'website' });
    this.metaService.updateTag({ property: 'og:image', content: ogImage });
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

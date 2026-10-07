import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { KampanyaIndirimi, KampanyaOzeti, efsaneCuma } from '../core/kampanya';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { PriceHistoryService } from '../core/price-history.service';
import { productPath } from '../core/product-link';
import { NewsletterSignup } from '../newsletter-signup/newsletter-signup';
import { SiteHeader } from '../site-header/site-header';

/**
 * Sezon sayfası (11.11 ve Efsane Cuma). Adres yılsız ve kalıcı: her yıl aynı sayfa güncelleniyor, biriken
 * otorite korunuyor; yıl başlıkta ve tarihlerde her istekte hesaplanıyor.
 *
 * Gerçek indirim ölçütü sitenin ürün kartlarındaki etiketten FARKLI: Fiyat Etiketi Yönetmeliği'nin "indirimden
 * önceki 30 günün en düşük fiyatı" (backend KampanyaIndirimServisi). Rapor yazısı da aynı ölçütü kullandığı için
 * iki sayfa aynı sayıyı gösteriyor; sayfa bu farkı SSS'de açıkça söylüyor.
 */
@Component({
  selector: 'app-efsane-kasim-page',
  imports: [DecimalPipe, RouterLink, NewsletterSignup, SiteHeader],
  templateUrl: './efsane-kasim-page.html',
})
export class EfsaneKasimPage implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);

  protected readonly yil = new Date().getFullYear();
  protected readonly onBirTarihi = new Date(Date.UTC(this.yil, 10, 11));
  protected readonly efsaneCumaTarihi = efsaneCuma(this.yil);

  protected readonly ozet = signal<KampanyaOzeti | null>(null);
  protected readonly yukleniyor = signal(true);
  protected readonly hata = signal(false);

  /** Fiyat geçmişini değerlendirebildiğimiz ürünler: indirim öncesi verisi yetersiz olanlar sayılmıyor. */
  protected readonly degerlendirilen = computed(() => {
    const o = this.ozet();
    return o ? o.gercek + o.ucuzlamamis + o.kalici : 0;
  });

  protected readonly displayName = displayName;
  protected readonly productPath = productPath;

  ngOnInit(): void {
    this.pageMeta.set({
      title: `Efsane Kasım ${this.yil}: Hangi Takviye İndirimi Gerçek? | Protein Avcısı`,
      description:
        `11.11 ve Efsane Cuma ${this.yil} takviye indirimleri: her indirimi ürünün indirimden önceki 30 gündeki en düşük ` +
        'fiyatıyla karşılaştırıyoruz. Gerçekten ucuzlayan ürünler tek sayfada.',
      canonicalPath: '/efsane-kasim',
    });
    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Efsane Kasım', path: '/efsane-kasim' },
      ]),
    );

    this.dealsService.getKampanyaOzeti().subscribe({
      next: (ozet) => {
        this.ozet.set(ozet);
        this.yukleniyor.set(false);
      },
      error: () => {
        this.hata.set(true);
        this.yukleniyor.set(false);
      },
    });
  }

  protected tarih(gun: Date): string {
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', weekday: 'long', timeZone: 'UTC' }).format(gun);
  }

  protected olcumZamani(iso: string): string {
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'long',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Europe/Istanbul',
    }).format(new Date(iso));
  }

  protected goToStoreUrl(indirim: KampanyaIndirimi): string {
    return this.priceHistoryService.goToStoreUrl(indirim.urun.productId, indirim.urun.storeUrl);
  }

  protected magazaTiklamasi(productId: number): void {
    this.priceHistoryService.trackStoreClick(productId);
  }
}

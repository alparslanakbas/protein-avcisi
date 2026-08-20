import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { slugify } from '../core/slugify';
import { SupplementDosage, findSupplementDosage } from '../core/supplement-dosages';
import { SiteHeader } from '../site-header/site-header';

interface DosageProduct {
  deal: Deal;
  totalGrams: number;
  daysSupply: number;
  costPerDay: number;
}

// Kreatin gibi geniş kategorilerde 8 satır marka çeşitliliğini gizliyordu
// (tablo en ucuz ürünlere göre sıralı; tek bir marka ilk sıraları
// doldurabiliyor).
const PRODUCT_LIMIT = 12;

// Kreatin/beta-alanine/sitrülin/betain/EAA için TEK bileşen, konfigürasyonla
// (supplement-dosages.ts) beş ayrı sayfa üretiyor. Her birine ayrı bileşen
// yazmak yüzlerce satır kod tekrarı olurdu; BrandPage'in iki modlu
// çalışmasıyla aynı yaklaşım.
//
// ÖNEMLİ TASARIM KARARI: bu takviyelerin dozu KİLOYA GÖRE ÖLÇEKLENMEZ —
// literatürde ve pratikte sabit aralıklar kullanılır. "Kilonu gir, dozunu
// hesaplayalım" tarzı bir araç uydurma olurdu. Bunun yerine dürüst bir doz
// aralığı gösterilip, asıl hesap bizim gerçek veri avantajımız üzerinden
// yapılıyor: seçilen paket kaç gün yeter, günlük maliyeti ne kadar.
@Component({
  selector: 'app-supplement-dosage-page',
  imports: [DecimalPipe, FormsModule, RouterLink, SiteHeader],
  templateUrl: './supplement-dosage-page.html',
})
export class SupplementDosagePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly config = signal<SupplementDosage | null>(null);
  protected readonly dailyGrams = signal<number>(0);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly products = signal<Deal[]>([]);

  // Seçilen günlük doza göre: her ürün kaç gün yeter, günlük maliyeti ne.
  // Yalnızca paket gramajı BİLİNEN ürünler listeleniyor — bilinmeyen için
  // tahmin yürütmüyoruz.
  protected readonly dosageProducts = computed<DosageProduct[]>(() => {
    const grams = this.dailyGrams();
    if (!grams || grams <= 0) return [];

    return this.products()
      .map((deal) => {
        const totalGrams = this.packageGrams(deal);
        if (!totalGrams) return null;

        const daysSupply = totalGrams / grams;
        if (daysSupply < 1) return null;

        return { deal, totalGrams, daysSupply, costPerDay: deal.currentPrice / daysSupply };
      })
      .filter((x): x is DosageProduct => x !== null)
      .sort((a, b) => a.costPerDay - b.costPerDay)
      .slice(0, PRODUCT_LIMIT);
  });

  protected productLink(deal: Deal): string[] {
    return ['/urun', String(deal.productId), slugify(deal.productName)];
  }

  // Paketteki toplam gram. İki kaynak var:
  // (1) Size alanı ("300 Gr" / "2 Kg") — üç markada bu geliyor;
  // (2) paket servis sayısı × servis gramajı — ProteinOcean'da Size hiç
  //     gelmiyor ama ikisi de markanın kendi verisinden geldiği için bu
  //     çarpım türetilmiş bir tahmin değil.
  // Bu ikinci yol olmadan ProteinOcean ürünleri tabloya hiç giremiyordu —
  // protein tozu tablosunda çözülen aynı sorun (bkz. CalculateServings).
  private packageGrams(deal: Deal): number | null {
    const fromSize = this.parsePackageGrams(deal.size);
    if (fromSize) return fromSize;

    if (deal.servingsPerPackage && deal.servingsPerPackage > 0 && deal.servingSizeGrams && deal.servingSizeGrams > 0) {
      return deal.servingsPerPackage * deal.servingSizeGrams;
    }

    return null;
  }

  private parsePackageGrams(size: string | null): number | null {
    if (!size) return null;
    const match = /^(\d+(?:[.,]\d+)?)\s*(Gr|Kg)$/i.exec(size.trim());
    if (!match) return null;

    const value = Number(match[1].replace(',', '.'));
    if (!value) return null;

    return match[2].toLowerCase() === 'kg' ? value * 1000 : value;
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug') ?? '';
      const config = findSupplementDosage(slug);

      if (!config) {
        // Soft-404 yerine gerçek yönlendirme — projedeki yerleşik desen.
        this.router.navigate(['/hesaplama']);
        return;
      }

      this.config.set(config);
      this.dailyGrams.set(config.defaultDailyGrams);
      this.setMeta(config);
      this.loadProducts(config);
    });
  }

  private setMeta(config: SupplementDosage): void {
    this.pageMeta.set({
      title: config.title,
      description: config.description,
      canonicalPath: `/hesaplama/${config.slug}`,
    });

    this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, {
      '@context': 'https://schema.org',
      '@type': 'WebApplication',
      name: config.h1,
      applicationCategory: 'HealthApplication',
      operatingSystem: 'Web',
      offers: { '@type': 'Offer', price: '0', priceCurrency: 'TRY' },
    });
  }

  private loadProducts(config: SupplementDosage): void {
    this.loading.set(true);
    this.loadError.set(false);

    this.dealsService
      // expandSynonyms: false — "alanine" araması, o kelime amino-asitler
      // kategorisinin anahtar kelimelerinden biri olduğu için kategorinin
      // TAMAMINI getiriyordu (arginin ürünleri beta-alanine sayfasında
      // listeleniyordu). Burada tam da o tek bileşeni arıyoruz.
      .getAllProducts({
        categories: config.category ? [config.category] : [],
        search: config.searchTerm ?? undefined,
        pageSize: 100,
        expandSynonyms: false,
      })
      .subscribe({
        next: (result) => {
          this.products.set(result.items);
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
  }
}

import { HttpErrorResponse } from '@angular/common/http';
import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { BrandComparison, CategoryComparison } from '../core/brand-comparison.model';
import { activitySentence, cheaperByKg, discountSentence, kgSummary } from '../core/brand-comparison-text';
import { BrandComparisonService } from '../core/brand-comparison.service';
import { brandSlug, resolveBrandFromSlug } from '../core/brand-slug';
import { DealsService } from '../core/deals.service';
import { CATEGORY_LABELS } from '../core/category-labels';
import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';
import { showNotFound } from '../core/not-found-navigation';
import { productPath } from '../core/product-link';

@Component({
  selector: 'app-brand-comparison-page',
  imports: [DecimalPipe, RouterLink, SiteHeader],
  templateUrl: './brand-comparison-page.html',
})
export class BrandComparisonPage implements OnInit {
  // Adres üretimi tek yerden: toLowerCase() marka adındaki boşluğu ve
  // Türkçe harfi adrese taşıyıp kanonikten sapan bir kopya üretiyordu.
  protected readonly brandSlug = brandSlug;
  protected readonly productPath = productPath;
  protected readonly cheaperByKg = cheaperByKg;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly comparisonService = inject(BrandComparisonService);
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private breadcrumbEl: HTMLScriptElement | null = null;

  protected readonly comparison = signal<BrandComparison | null>(null);
  protected readonly loading = signal(true);
  protected readonly pairSlug = signal('');

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const pair = params.get('pair') ?? '';
      this.loadComparison(pair);
    });
  }

  private loadComparison(pair: string): void {
    this.loading.set(true);
    const parts = pair.split('-vs-');
    if (parts.length !== 2 || !parts[0] || !parts[1]) {
      showNotFound(this.router);
      return;
    }

    this.pairSlug.set(pair);

    // Adresteki parça bir slug ("torq-nutrition"); API gerçek marka adını
    // bekliyor. Marka listesinden eşleştiriliyor, bulunamazsa parça olduğu
    // gibi gönderiliyor — boşluklu eski adresler böyle çalışmaya devam ediyor.
    this.dealsService.getFilterOptions().subscribe({
      next: (filters) => {
        const brand1 = resolveBrandFromSlug(parts[0], filters.brands) ?? parts[0];
        const brand2 = resolveBrandFromSlug(parts[1], filters.brands) ?? parts[1];
        this.compareBrands(brand1, brand2);
      },
      error: () => this.compareBrands(parts[0], parts[1]),
    });
  }

  private compareBrands(brand1: string, brand2: string): void {
    this.comparisonService.compare(brand1, brand2).subscribe({
      next: (result) => {
        this.comparison.set(result);
        this.setMeta(result);
        this.loading.set(false);
      },
      // 404 ile GEÇİCİ hata ayrılıyor. Önceden her hata ana sayfaya
      // yönlendiriyordu; backend'in bir anlık 5xx'i yüzünden arama motoruna
      // "bu sayfa yok" demek kalıcı zarar verirdi. Bu sayfada henüz bir hata
      // ekranı yok, o yüzden geçici hatada eski davranış korunuyor.
      error: (err: HttpErrorResponse) => {
        if (err.status === 404) {
          showNotFound(this.router);
          return;
        }
        void this.router.navigate(['/']);
      },
    });
  }

  private setMeta(comparison: BrandComparison): void {
    const title = `${comparison.brand1} vs ${comparison.brand2} Fiyat Karşılaştırması | Protein Avcısı`;
    const description = `${comparison.brand1} ve ${comparison.brand2}: kategori bazında kilogram fiyatı, bugünkü gerçek indirimler ve son 30 günün fiyat hareketi — kendi fiyat kayıtlarımızdan.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/karsilastir/${this.pairSlug()}`,
    });

    // Sitenin en yüksek TO'lu sayfa türü (6 Ekim GSC: %6,5) sayfaya özgü
    // hiçbir yapılandırılmış veri taşımıyordu (SEO denetimi, 10. madde).
    this.breadcrumbEl = upsertJsonLdScript(
      this.document,
      this.breadcrumbEl,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Markalar', path: '/markalar' },
        { name: `${comparison.brand1} vs ${comparison.brand2}`, path: `/karsilastir/${this.pairSlug()}` },
      ]),
    );
  }

  protected categoryLabel(category: string): string {
    return CATEGORY_LABELS[category] ?? category;
  }

  // Yorumlar core/brand-comparison-text.ts'te, saf ve test edilebilir. "Daha ucuz"
  // kararı yalnız kilogram fiyatıyla (20 Ekim): ham ortalama paket boyutuna bakmıyordu.
  // Önceki sürümün "leader" ilkesi korunuyor: kazanan marka cümlede tek ve net özne.
  protected readonly summary = computed(() => {
    const c = this.comparison();
    return c && c.categories.length > 0 ? kgSummary(c.categories) : null;
  });

  protected readonly activityText = computed(() => {
    const c = this.comparison();
    return c ? activitySentence(c) : null;
  });

  protected readonly discountText = computed(() => {
    const c = this.comparison();
    return c ? discountSentence(c) : null;
  });

  /** İki markada da kilogram başına en uygun ürünü olan kategoriler. */
  protected readonly bestPicks = computed(() =>
    (this.comparison()?.categories ?? []).filter((cat) => cat.brand1BestPick && cat.brand2BestPick),
  );

  protected hasKg(cat: CategoryComparison): boolean {
    return cat.brand1MedianPerKg !== null || cat.brand2MedianPerKg !== null;
  }
}

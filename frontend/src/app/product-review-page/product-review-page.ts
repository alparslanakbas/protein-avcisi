import { DOCUMENT, DecimalPipe, isPlatformServer } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, PLATFORM_ID, RESPONSE_INIT, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { canonicalOrigin } from '../core/canonical-link';
import { CATEGORY_LABELS } from '../core/category-labels';
import { CategoryPriceStats } from '../core/category-price-stats.model';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { PricePoint } from '../core/price-history.model';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { slugify } from '../core/slugify';
import { buildAreaPath, buildLinePath, toCoordinates } from '../core/spark-chart';
import { SiteHeader } from '../site-header/site-header';

const CHART = { width: 640, height: 160, paddingY: 16 };
const HISTORY_DAYS = 30;
const SIMILAR_PRODUCTS_LIMIT = 4;

interface NutritionRow {
  label: string;
  value: string;
}

// Ürün incelemesi sayfası — rakip analizinde görülen bir fırsat
// (rakipte sadece 2 tane var, biz 500+ ürünle bu alanı domine edebiliriz).
// BİLİNÇLİ SINIR: rakibin yaptığı gibi "test ettik, şöyle hissettirdi" öznel
// bir iddia YAPMIYORUZ — elimizde gerçek kullanım deneyimi yok. Bunun yerine
// tamamen kendi verimize (fiyat geçmişi, besin değeri, kategori konumu)
// dayanan nesnel bir analiz sunuyoruz. Eksik veri SESSİZCE gizlenmiyor —
// kullanıcı isteğiyle: markadan gelmeyen her alan için dürüst bir not
// gösteriliyor ("bu bilgi X markası tarafından paylaşılmıyor" gibi),
// taraflı görünmeyelim diye.
@Component({
  selector: 'app-product-review-page',
  imports: [DecimalPipe, RouterLink, SiteHeader],
  templateUrl: './product-review-page.html',
})
export class ProductReviewPage implements OnInit {
  protected readonly displayName = displayName;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly isServer = isPlatformServer(inject(PLATFORM_ID));

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly deal = signal<Deal | null>(null);
  protected readonly points = signal<PricePoint[]>([]);
  protected readonly categoryStats = signal<CategoryPriceStats | null>(null);
  protected readonly similarProducts = signal<Deal[]>([]);

  protected readonly chart = CHART;
  protected readonly historyDays = HISTORY_DAYS;

  protected readonly servings = computed(() => this.calculateServings(this.deal()));
  protected readonly pricePerServing = computed(() => {
    const d = this.deal();
    const s = this.servings();
    return d && s && s >= 1 ? d.currentPrice / s : null;
  });

  protected readonly nutritionRows = computed<NutritionRow[]>(() => {
    const json = this.deal()?.nutritionJson;
    if (!json) return [];
    try {
      const parsed = JSON.parse(json) as Record<string, string>;
      return Object.entries(parsed).map(([label, value]) => ({ label, value }));
    } catch {
      return [];
    }
  });

  // Bu ürünün kategori ortalamasına göre konumu — uydurma bir "puan" değil,
  // gerçek fiyat farkının yüzdesi. Ortalama 0'sa (teorik olarak imkansız
  // ama savunmacı) hesap yapılmıyor.
  protected readonly categoryPricePosition = computed(() => {
    const d = this.deal();
    const stats = this.categoryStats();
    if (!d || !stats || stats.averagePrice <= 0) return null;

    const diffPercent = Math.round(((d.currentPrice - stats.averagePrice) / stats.averagePrice) * 100);
    return { diffPercent, isCheaper: diffPercent < 0 };
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (!Number.isInteger(id) || id <= 0) {
        this.router.navigate(['/']);
        return;
      }
      this.load(id);
    });
  }

  private load(id: number): void {
    this.loading.set(true);
    this.loadError.set(false);

    forkJoin({
      deal: this.dealsService.getProductById(id),
      history: this.priceHistoryService.get(id, HISTORY_DAYS).pipe(catchError(() => of({ points: [] as PricePoint[] }))),
    }).subscribe({
      next: ({ deal, history }) => {
        this.deal.set(deal);
        this.points.set(history.points);
        this.setMeta(deal);
        this.loading.set(false);

        if (deal.category) {
          this.dealsService.getCategoryPriceStats(deal.category).subscribe((stats) => this.categoryStats.set(stats));
          this.dealsService
            .getAllProducts({ categories: [deal.category], pageSize: SIMILAR_PRODUCTS_LIMIT + 1 })
            .subscribe((result) => this.similarProducts.set(result.items.filter((d) => d.productId !== id).slice(0, SIMILAR_PRODUCTS_LIMIT)));
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        // deals-list.ts / product-comparison-page.ts'teki aynı ayrım:
        // ürün gerçekten yoksa (404) ana sayfaya, geçici bir hataysa 503.
        if (err.status === 404) {
          this.router.navigate(['/']);
          return;
        }
        this.loadError.set(true);
        if (this.responseInit) this.responseInit.status = 503;
      },
    });
  }

  // Backend'deki DealsQueryService.CalculateServings ile aynı öncelik.
  private calculateServings(deal: Deal | null): number | null {
    if (!deal) return null;
    if (deal.servingsPerPackage && deal.servingsPerPackage > 0) return deal.servingsPerPackage;

    const packageGrams = this.parsePackageGrams(deal.size);
    if (packageGrams && deal.servingSizeGrams && deal.servingSizeGrams > 0) {
      return packageGrams / deal.servingSizeGrams;
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

  protected chartPath(): string {
    return buildLinePath(this.coordinates());
  }

  protected chartArea(): string {
    return buildAreaPath(this.coordinates(), CHART.height);
  }

  private coordinates() {
    const pts = this.points();
    if (pts.length === 0) return [];
    const prices = pts.map((p) => p.price);
    return toCoordinates(pts, Math.min(...prices), Math.max(...prices), CHART);
  }

  protected categoryLabel(slug: string | null): string {
    if (!slug) return '—';
    return CATEGORY_LABELS[slug] ?? slug;
  }

  protected lastChecked(): string {
    const d = this.deal();
    return d ? formatRelativeTime(d.scrapedAt) : '';
  }

  protected storeUrl(): string {
    const d = this.deal();
    return d ? this.priceHistoryService.goToStoreUrl(d.productId) : '#';
  }

  protected productLink(d: Deal): string[] {
    return ['/urun', String(d.productId), slugify(d.productName)];
  }

  private setMeta(deal: Deal): void {
    const slug = slugify(deal.productName);
    const name = displayName(deal.productName);
    const title = `${name} İncelemesi 2026 | ${deal.brandName}`;
    const description = `${name} için gerçek fiyat geçmişi, besin değeri ve kategori karşılaştırmasına dayanan bağımsız inceleme. ProteinAvcısı, marka beyanına değil kendi verisine güvenir.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/urun-inceleme/${deal.productId}/${slug}`,
      ogType: 'article',
      ogImage: deal.imageUrl ?? undefined,
    });

    const origin = canonicalOrigin(this.document);
    const jsonLd = {
      '@context': 'https://schema.org',
      '@type': 'Product',
      name,
      sku: String(deal.productId),
      ...(deal.imageUrl ? { image: deal.imageUrl } : {}),
      brand: { '@type': 'Brand', name: deal.brandName },
      offers: {
        '@type': 'Offer',
        url: `${origin}/urun/${deal.productId}/${slug}`,
        priceCurrency: 'TRY',
        price: deal.currentPrice.toFixed(2),
      },
      ...(this.nutritionRows().length > 0
        ? {
            additionalProperty: this.nutritionRows().map((r) => ({
              '@type': 'PropertyValue',
              name: r.label,
              value: r.value,
            })),
          }
        : {}),
    };
    upsertJsonLdScript(this.document, null, jsonLd);

    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        ...(deal.category ? [{ name: this.categoryLabel(deal.category), path: `/kategori/${deal.category}` }] : []),
        { name: `${name} İncelemesi`, path: `/urun-inceleme/${deal.productId}/${slug}` },
      ]),
    );
  }
}

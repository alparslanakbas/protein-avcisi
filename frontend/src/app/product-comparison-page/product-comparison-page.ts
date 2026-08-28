import { DOCUMENT, DecimalPipe, isPlatformServer } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, PLATFORM_ID, RESPONSE_INIT, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CATEGORY_LABELS } from '../core/category-labels';
import { ComparisonService } from '../core/comparison.service';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { PROTEIN_REFERENCE_GRAMS, proteinRatioPercent, proteinReferenceCost } from '../core/value-metrics';
import { PageMetaService } from '../core/page-meta.service';
import { PricePoint } from '../core/price-history.model';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { slugify } from '../core/slugify';
import { buildAreaPath, buildLinePath, toCoordinates } from '../core/spark-chart';
import { SiteHeader } from '../site-header/site-header';

// Karşılaştırma grafiği — iki ürün için aynı ölçüler, yan yana okunabilsin.
const CHART = { width: 320, height: 100, paddingY: 10 };
const HISTORY_DAYS = 30;

interface ComparedProduct {
  deal: Deal;
  points: PricePoint[];
  servings: number | null;
  pricePerServing: number | null;
  // Porsiyonun yüzde kaçı protein ve sabit 25 g proteinin maliyeti —
  // paket boyutundan arındırılmış, iki ürünü doğrudan kıyaslayan ölçüler
  // (bkz. core/value-metrics.ts).
  proteinRatio: number | null;
  proteinCost: number | null;
}

// İki ürünü yan yana karşılaştıran sayfa. Verinin TAMAMI taze çekiliyor —
// alt çubuktaki seçim yalnızca hangi ürünler olduğunu taşıyor, fiyat gibi
// alanlar oradan okunmuyor (bayatlamış olabilir).
//
// SEO NOTU: bu sayfalar sitemap'e EKLENMİYOR. 600 üründen ~180 bin çift
// çıkıyor; hepsini taranmaya sunmak, GSC'de zaten uğraştığımız "keşfedildi
// ama indekslenmedi" sorununu büyütürdü. Paylaşılan linkler yine çalışıyor
// ve sunucuda render ediliyor.
@Component({
  selector: 'app-product-comparison-page',
  imports: [DecimalPipe, RouterLink, SiteHeader],
  templateUrl: './product-comparison-page.html',
})
export class ProductComparisonPage implements OnInit {
  protected readonly displayName = displayName;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly isServer = isPlatformServer(inject(PLATFORM_ID));
  protected readonly comparison = inject(ComparisonService);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly products = signal<ComparedProduct[]>([]);

  protected readonly chart = CHART;
  protected readonly historyDays = HISTORY_DAYS;

  // İki üründen hangisinin daha ucuz olduğu gibi karşılaştırmalar; eşitse
  // hiçbiri vurgulanmıyor.
  protected readonly cheaperIndex = computed(() => this.betterIndex((p) => p.deal.currentPrice, 'min'));
  protected readonly cheaperPerServingIndex = computed(() =>
    this.betterIndex((p) => p.pricePerServing, 'min'),
  );
  protected readonly biggerPackageIndex = computed(() => this.betterIndex((p) => p.servings, 'max'));
  // Protein oranında YÜKSEK, sabit protein maliyetinde DÜŞÜK olan iyidir.
  protected readonly denserProteinIndex = computed(() => this.betterIndex((p) => p.proteinRatio, 'max'));
  protected readonly cheaperProteinIndex = computed(() => this.betterIndex((p) => p.proteinCost, 'min'));
  protected readonly proteinReferenceGrams = PROTEIN_REFERENCE_GRAMS;

  // Her iki ürünün besin değeri tablosunu (varsa) tek bir satır listesine
  // birleştiriyor — sıra, A ürününün kendi tablosundaki sırayı korur, B'de
  // olup A'da olmayan satırlar sona ekleniyor. Bir markanın vermediği satır
  // (JSON'da hiç yoksa) "—" gösteriyor, uydurma yok.
  protected readonly nutritionRows = computed(() => {
    const list = this.products();
    if (list.length < 2) return [];

    const tableA = this.parseNutrition(list[0].deal.nutritionJson);
    const tableB = this.parseNutrition(list[1].deal.nutritionJson);
    if (!tableA && !tableB) return [];

    const labels = [...Object.keys(tableA ?? {}), ...Object.keys(tableB ?? {})];
    const seen = new Set<string>();
    return labels
      .filter((label) => (seen.has(label) ? false : (seen.add(label), true)))
      .map((label) => ({ label, values: [tableA?.[label] ?? null, tableB?.[label] ?? null] as [string | null, string | null] }));
  });

  private parseNutrition(json: string | null): Record<string, string> | null {
    if (!json) return null;
    try {
      return JSON.parse(json) as Record<string, string>;
    } catch {
      return null;
    }
  }

  private betterIndex(pick: (p: ComparedProduct) => number | null, mode: 'min' | 'max'): number | null {
    const list = this.products();
    if (list.length < 2) return null;

    const a = pick(list[0]);
    const b = pick(list[1]);
    if (a == null || b == null || a === b) return null;

    const firstWins = mode === 'min' ? a < b : a > b;
    return firstWins ? 0 : 1;
  }

  protected chartPath(points: PricePoint[]): string {
    return buildLinePath(this.coordinates(points));
  }

  protected chartArea(points: PricePoint[]): string {
    return buildAreaPath(this.coordinates(points), CHART.height);
  }

  private coordinates(points: PricePoint[]) {
    if (points.length === 0) return [];
    const prices = points.map((p) => p.price);
    return toCoordinates(points, Math.min(...prices), Math.max(...prices), CHART);
  }

  protected productLink(deal: Deal): string[] {
    return ['/urun', String(deal.productId), slugify(deal.productName)];
  }

  protected categoryLabel(slug: string | null): string {
    if (!slug) return '—';
    return CATEGORY_LABELS[slug] ?? slug;
  }

  protected lastChecked(deal: Deal): string {
    return formatRelativeTime(deal.scrapedAt);
  }

  protected storeUrl(productId: number): string {
    return this.priceHistoryService.goToStoreUrl(productId);
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const ids = this.parsePair(params.get('pair'));
      if (!ids) {
        this.router.navigate(['/']);
        return;
      }

      this.load(ids[0], ids[1]);
    });
  }

  // "29-vs-603" → [29, 603]. Geçersiz biçim null döner (sayfa ana sayfaya
  // yönlenir; soft-404 yerine gerçek yönlendirme — projedeki yerleşik desen).
  private parsePair(pair: string | null): [number, number] | null {
    if (!pair) return null;

    const parts = pair.split('-vs-');
    if (parts.length !== 2) return null;

    const a = Number(parts[0]);
    const b = Number(parts[1]);
    if (!Number.isInteger(a) || !Number.isInteger(b) || a <= 0 || b <= 0 || a === b) return null;

    return [a, b];
  }

  private load(idA: number, idB: number): void {
    this.loading.set(true);
    this.loadError.set(false);

    forkJoin({
      dealA: this.dealsService.getProductById(idA),
      dealB: this.dealsService.getProductById(idB),
      historyA: this.priceHistoryService.get(idA, HISTORY_DAYS).pipe(catchError(() => of({ points: [] as PricePoint[] }))),
      historyB: this.priceHistoryService.get(idB, HISTORY_DAYS).pipe(catchError(() => of({ points: [] as PricePoint[] }))),
    }).subscribe({
      next: ({ dealA, dealB, historyA, historyB }) => {
        this.products.set([
          this.build(dealA, historyA.points),
          this.build(dealB, historyB.points),
        ]);
        this.setMeta(dealA, dealB);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);

        // Ürünlerden biri gerçekten yoksa (404) bu çift hiçbir zaman geçerli
        // olmayacak — ana sayfaya yönlendiriyoruz. Geçici bir hataysa (ağ
        // hatası, backend 5xx) 503 ile "sonra tekrar dene" diyoruz; kalıcı
        // olmayan bir sorun için "artık yok" sinyali vermiyoruz.
        // deals-list.ts'teki aynı ayrım.
        if (err.status === 404) {
          this.router.navigate(['/']);
          return;
        }

        this.loadError.set(true);
        if (this.responseInit) this.responseInit.status = 503;
      },
    });
  }

  private build(deal: Deal, points: PricePoint[]): ComparedProduct {
    const servings = this.calculateServings(deal);
    return {
      deal,
      points,
      servings,
      pricePerServing: servings && servings >= 1 ? deal.currentPrice / servings : null,
      proteinRatio: proteinRatioPercent(deal),
      proteinCost: proteinReferenceCost(deal),
    };
  }

  // Backend'deki DealsQueryService.CalculateServings ile aynı öncelik:
  // markanın doğrudan beyan ettiği servis sayısı varsa o, yoksa paket
  // gramajı ÷ porsiyon büyüklüğü.
  private calculateServings(deal: Deal): number | null {
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

  private setMeta(a: Deal, b: Deal): void {
    const nameA = displayName(a.productName);
    const nameB = displayName(b.productName);
    const title = `${nameA} vs ${nameB} — Fiyat Karşılaştırması | ProteinAvcısı`;
    this.pageMeta.set({
      title,
      description: `${a.brandName} ${nameA} ile ${b.brandName} ${nameB} ürünlerini güncel fiyat, servis başı maliyet ve 30 günlük fiyat geçmişiyle yan yana karşılaştır.`,
      canonicalPath: `/karsilastir-urun/${ComparisonService.pairSlug(a.productId, b.productId)}`,
    });
  }
}

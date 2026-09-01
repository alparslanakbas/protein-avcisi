import { DOCUMENT, DecimalPipe, isPlatformServer } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, PLATFORM_ID, RESPONSE_INIT, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { StoreLinkTargetDirective } from '../core/store-link-target.directive';
import { dedupeSameDaySamePrice, hoverAlign, nearestPointIndex, tooltipDateLabel } from '../core/chart-hover';
import { buildPageTitle, clampDescription } from '../core/meta-description';
import { buildProductFacts, buildProductJsonLdDescription } from '../core/product-facts';
import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { canonicalOrigin } from '../core/canonical-link';
import { CATEGORY_LABELS } from '../core/category-labels';
import { CategoryPriceStats } from '../core/category-price-stats.model';
import { ComparisonService } from '../core/comparison.service';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { PricePoint } from '../core/price-history.model';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { slugify } from '../core/slugify';
import { PROTEIN_REFERENCE_GRAMS, proteinRatioPercent, proteinReferenceCost } from '../core/value-metrics';
import { buildAreaPath, buildLinePath, toCoordinates } from '../core/spark-chart';
import { SiteHeader } from '../site-header/site-header';

const CHART = { width: 640, height: 160, paddingY: 16 };
const HISTORY_DAYS = 30;
const SIMILAR_PRODUCTS_LIMIT = 4;
const BEST_VALUE_LIMIT = 3;
const CLOSEST_ALTERNATIVES_LIMIT = 2;

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
  imports: [StoreLinkTargetDirective, DecimalPipe, RouterLink, SiteHeader],
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

  // Markanın tanıtım metninin yerini alan, tamamen kendi verimizden türeyen
  // bilgi listesi — gerekçe için core/product-facts.ts'teki nota bak.
  protected readonly productFacts = computed(() => {
    const deal = this.deal();
    return deal ? buildProductFacts(deal) : [];
  });
  protected readonly points = signal<PricePoint[]>([]);
  protected readonly categoryStats = signal<CategoryPriceStats | null>(null);
  protected readonly similarProducts = signal<Deal[]>([]);
  // "Bu kategoride servis başı en uygun ürünler" mini-tablosu —
  // /api/best-value-per-serving zaten servis başı fiyata göre sıralı
  // döndürüyor (aynı hesaplayıcı sayfasının kullandığı uç nokta), burada
  // mevcut ürün listeden çıkarılıp ilk 3'ü alınıyor. Amaç: "GI+ vs X" gibi
  // karşılaştırma niyetini de bu sayfada karşılamak (dış bir kod
  // incelemesinde önerildi, kullanıcı onayladı) — canonical'ı /urun'a
  // birleştirip bu sayfayı Google'dan gizlemek yerine, gerçekten
  // farklılaştırıp indekslenebilir tutmak.
  protected readonly bestValueInCategory = signal<Deal[]>([]);

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
        // Aynı gün + aynı fiyat tekrarlarını ele — yoksa hover art arda
        // aynı tarihi gösteriyor (modalda yaşanan hatanın aynısı).
        this.points.set(dedupeSameDaySamePrice(history.points));
        this.setMeta(deal);
        this.loading.set(false);

        this.bestValueInCategory.set([]);
        if (deal.category) {
          this.dealsService.getCategoryPriceStats(deal.category).subscribe((stats) => this.categoryStats.set(stats));
          this.dealsService
            .getAllProducts({ categories: [deal.category], pageSize: SIMILAR_PRODUCTS_LIMIT + 1 })
            .subscribe((result) => this.similarProducts.set(result.items.filter((d) => d.productId !== id).slice(0, SIMILAR_PRODUCTS_LIMIT)));
          this.dealsService
            .getBestValuePerServing({ category: deal.category, pageSize: BEST_VALUE_LIMIT + 1 })
            .subscribe({
              next: (result) => this.bestValueInCategory.set(result.items.filter((d) => d.productId !== id).slice(0, BEST_VALUE_LIMIT)),
              // Kategoride porsiyon verisi olan hiçbir ürün yoksa endpoint
              // 404 dönebilir — bölüm bu durumda sessizce görünmüyor,
              // ana içerik (fiyat/besin değeri) etkilenmiyor.
              error: () => this.bestValueInCategory.set([]),
            });
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

  // Servis başı fiyat — herhangi bir Deal için (mevcut ürün ile
  // sınırlı olan yukarıdaki servings/pricePerServing computed'lerinin
  // aksine, mini-karşılaştırma tablosundaki HER satır için ayrı ayrı
  // hesaplanması gerekiyor).
  protected pricePerServingFor(deal: Deal): number | null {
    const servings = this.calculateServings(deal);
    return servings && servings >= 1 ? deal.currentPrice / servings : null;
  }

  // Sabit miktarda proteinin maliyeti. Hesap artık paylaşılan modülde
  // (core/value-metrics.ts) — karşılaştırma sayfası da aynı fonksiyonu
  // kullanıyor, böylece iki sayfa aynı ürün için farklı rakam gösteremiyor.
  protected readonly proteinReferenceGrams = PROTEIN_REFERENCE_GRAMS;

  protected proteinCostPerServing30g(deal: Deal): number | null {
    return proteinReferenceCost(deal);
  }

  // Porsiyonun yüzde kaçı protein — "ödediğin paranın ne kadarı etken
  // maddeye gidiyor" sorusunun cevabı.
  protected proteinRatio(deal: Deal): number | null {
    return proteinRatioPercent(deal);
  }

  // "Servis başına en uygun" tablosu (bestValueInCategory) boş kaldığında
  // — ürünün kategorisi yok, ya da kategoride porsiyon verisi olan hiçbir
  // ürün yok — TAMAMEN İNCE bir sayfa yerine en azından FİYATA dayalı bir
  // karşılaştırma göstermek için (dış bir kod incelemesinde bulundu: "GI+
  // incelemesinde hiçbir karşılaştırma bloğu çıkmıyor"). similarProducts
  // zaten kategori bazlı çekiliyor (servis verisi şartı olmadan), burada
  // sadece fiyata göre sıralanıp ilk 3'ü alınıyor.
  protected readonly priceFallbackProducts = computed(() =>
    [...this.similarProducts()].sort((a, b) => a.currentPrice - b.currentPrice).slice(0, 3),
  );

  // Aynı kategorideki ürünler arasından mevcut ürüne FİYATÇA en yakın
  // olanları — "en yakın alternatif" burada bilinçli olarak sadece
  // sayısal bir yakınlık, öznel bir "benzer ürün" yorumu değil.
  protected readonly closestAlternatives = computed(() => {
    const current = this.deal();
    if (!current) return [];
    return [...this.similarProducts()]
      .sort((a, b) => Math.abs(a.currentPrice - current.currentPrice) - Math.abs(b.currentPrice - current.currentPrice))
      .slice(0, CLOSEST_ALTERNATIVES_LIMIT);
  });

  // Bir alternatifin mevcut ürüne göre farkını dürüst bir cümleyle özetliyor
  // — sadece ölçülebilir farklar (fiyat, indirim durumu, servis başı fiyat),
  // hiçbir öznel "daha iyi/kötü" yorumu yok.
  protected comparisonNote(alt: Deal): string {
    const current = this.deal();
    if (!current) return '';

    const parts: string[] = [];
    const priceDiff = alt.currentPrice - current.currentPrice;
    if (Math.abs(priceDiff) >= 1) {
      parts.push(priceDiff < 0 ? `${Math.abs(priceDiff).toFixed(0)} ₺ daha ucuz` : `${priceDiff.toFixed(0)} ₺ daha pahalı`);
    }

    const altPerServing = this.pricePerServingFor(alt);
    const currentPerServing = this.pricePerServingFor(current);
    if (altPerServing && currentPerServing && Math.abs(altPerServing - currentPerServing) >= 0.5) {
      parts.push(altPerServing < currentPerServing ? 'servis başına daha uygun' : 'servis başına daha pahalı');
    }

    if (alt.discountPercent > 0 && current.discountPercent === 0) {
      parts.push(`şu an %${alt.discountPercent} indirimde`);
    }

    return parts.length > 0 ? parts.join(', ') : 'fiyatı neredeyse aynı';
  }

  // /karsilastir-urun/{id}-vs-{id} — mevcut ürün karşılaştırma sayfasıyla
  // aynı kanonik (alfabetik) URL kuralı.
  protected comparisonLink(other: Deal): string[] {
    const current = this.deal();
    if (!current) return ['/'];
    return ['/karsilastir-urun', ComparisonService.pairSlug(current.productId, other.productId)];
  }

  protected chartPath(): string {
    return buildLinePath(this.coordinates());
  }

  protected chartArea(): string {
    return buildAreaPath(this.coordinates(), CHART.height);
  }

  private coordinates(): [number, number][] {
    const pts = this.points();
    if (pts.length === 0) return [];
    const prices = pts.map((p) => p.price);
    return toCoordinates(pts, Math.min(...prices), Math.max(...prices), CHART);
  }

  // --- Grafik etkileşimi ---
  // Önceden bu sayfada grafik yalnızca çizgiydi; üzerine gelince hiçbir bilgi
  // vermiyordu. Hesaplar core/chart-hover.ts'te paylaşılıyor — ürün modalında
  // aynı mantık üç ayrı üretim hatası vermişti, ikinci bir kopya o hataların
  // geri gelmesini garanti ederdi.
  protected readonly hoverIndex = signal<number | null>(null);

  protected readonly hoverInfo = computed(() => {
    const idx = this.hoverIndex();
    if (idx === null) return null;
    const coords = this.coordinates();
    const pts = this.points();
    if (idx >= coords.length || idx >= pts.length) return null;

    const [x, y] = coords[idx];
    return {
      x,
      y,
      align: hoverAlign(x, CHART.width),
      price: pts[idx].price,
      dateLabel: tooltipDateLabel(pts, idx),
    };
  });

  protected onChartMouseMove(event: MouseEvent): void {
    this.updateHover(event.currentTarget as SVGSVGElement, event.clientX);
  }

  protected onChartMouseLeave(): void {
    this.hoverIndex.set(null);
  }

  // Mobilde mousemove tetiklenmiyor; parmağın grafiği kesintisiz takip etmesi
  // için touch olaylarına ayrıca bağlanıyor. touchend'de bilinçli olarak
  // sıfırlanmıyor — parmak kalkınca son değer görünür kalıyor.
  protected onChartTouchMove(event: TouchEvent): void {
    const touch = event.touches[0];
    if (!touch) return;
    // Grafik içindeyken sayfanın kaymasını engelle.
    event.preventDefault();
    this.updateHover(event.currentTarget as SVGSVGElement, touch.clientX);
  }

  private updateHover(svg: SVGSVGElement, clientX: number): void {
    this.hoverIndex.set(nearestPointIndex(svg, clientX, this.coordinates(), CHART.width));
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
    // Yıl bilinçli olarak title'da YOK — hardcode "2026" 2027'de tüm
    // title'ları bakımsız/yalan gösterirdi, Google zaten tarihi lastmod/
    // yayın tarihinden okuyor (dış kod incelemesinde bulundu).
    const title = buildPageTitle(name, 'İncelemesi', deal.brandName);
    // Google ~155 karakterde kesiyor; kuyruktaki jenerik cümle zaten
    // görünmüyordu (denetimde 238 karaktere kadar çıkan örnek vardı).
    const description = clampDescription(
      `${name} için gerçek fiyat geçmişi, besin değeri ve kategori karşılaştırmasına dayanan bağımsız inceleme.`,
    );

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
      description: buildProductJsonLdDescription(deal),
      offers: {
        '@type': 'Offer',
        url: `${origin}/urun/${deal.productId}/${slug}`,
        priceCurrency: 'TRY',
        price: deal.currentPrice.toFixed(2),
      },
      ...(deal.ratingValue !== null && deal.ratingCount !== null
        ? {
            aggregateRating: {
              '@type': 'AggregateRating',
              ratingValue: deal.ratingValue,
              reviewCount: deal.ratingCount,
              bestRating: 5,
            },
          }
        : {}),
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

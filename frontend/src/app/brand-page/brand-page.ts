import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { buildBrandFaqs } from '../core/brand-faqs';
import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { BrandStats } from '../core/brand-stats.model';
import { CATEGORY_LABELS } from '../core/category-labels';
import { ComparisonService } from '../core/comparison.service';
import { Coupon } from '../core/coupon.model';
import { CouponsService } from '../core/coupons.service';
import { Deal } from '../core/deal.model';
import { productPath, shouldHandleInApp } from '../core/product-link';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { PricePoint } from '../core/price-history.model';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { ProductCardSparkline } from '../product-card-sparkline/product-card-sparkline';
import { ProductModal } from '../product-modal/product-modal';
import { SiteHeader } from '../site-header/site-header';

type ViewMode = 'deals' | 'store' | 'all';
const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 350;

@Component({
  selector: 'app-brand-page',
  imports: [DecimalPipe, FormsModule, RouterLink, ProductCardSparkline, ProductModal, SiteHeader],
  templateUrl: './brand-page.html',
})
export class BrandPage implements OnInit {
  protected readonly displayName = displayName;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly couponsService = inject(CouponsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private readonly priceHistoryService = inject(PriceHistoryService);
  protected readonly comparison = inject(ComparisonService);
  private breadcrumbEl: HTMLScriptElement | null = null;
  private faqEl: HTMLScriptElement | null = null;

  // Markaya özel SSS — yalnızca marka ana sayfasında (marka × kategori
  // kesişiminde değil), çünkü "indirim kodu" sorguları oraya gelmiyor.
  // Kupon durumuna ve marka istatistiklerine bağlı olduğu için computed.
  protected readonly brandFaqs = computed(() => {
    if (this.fixedCategory()) return [];
    const brand = this.brandName();
    if (!brand) return [];
    const stats = this.brandStats();
    return buildBrandFaqs({
      brandName: brand,
      couponCodes: this.coupons().map((c) => c.code),
      totalProducts: stats?.totalProducts ?? null,
      averageDiscountPercent: stats?.averageDiscountPercent ?? null,
      topCategoryLabel: this.topCategoryLabel(),
    });
  });

  protected readonly brandName = signal<string>('');
  protected readonly coupons = signal<Coupon[]>([]);
  protected readonly otherBrands = signal<string[]>([]);

  // Marka × kategori kesişim sayfası (/marka/:brandSlug/:categorySlug).
  // Doluysa kategori SABİT: çip listesi gizleniyor, başlık/meta/canonical
  // o kategoriye özel oluyor. Boşsa sayfa eski haliyle (tüm kategoriler,
  // çiplerle filtrelenebilir) çalışıyor — tek bileşen, iki mod
  // (DealsList'in '/' ve '/urun/:id'yi paylaşmasıyla aynı desen).
  protected readonly fixedCategory = signal<string | null>(null);
  protected readonly fixedCategoryLabel = signal<string>('');
  // Bu markanın gerçekten ürünü olan kategorileri — sayfa altındaki iç
  // linkler için (boş kombinasyona link vermemek adına).
  protected readonly brandCategories = signal<{ slug: string; label: string; count: number }[]>([]);
  // Bu markaya özgün, kendi verimize dayanan istatistik bölümü — sadece
  // markanın kendi (kesişim değil) sayfasında gösteriliyor.
  protected readonly brandStats = signal<BrandStats | null>(null);
  protected readonly topCategoryLabel = computed(() => {
    const cats = this.brandCategories();
    if (cats.length === 0) return null;
    return [...cats].sort((a, b) => b.count - a.count)[0].label;
  });
  protected readonly loading = signal(true);
  // Adı "notFound" değil "loadError" — bu yalnızca /api/filters isteği
  // BAŞARISIZ olunca set ediliyor (network/API hatası). Geçersiz bir marka
  // slug'ı zaten aşağıda gerçek bir yönlendirmeyle ('/') ele alınıyor, hiç
  // bu duruma düşmüyor — eski "Bu marka bulunamadı" metni bu yüzden
  // yanıltıcıydı, gerçek bir yükleme hatasını "marka yok" gibi gösteriyordu.
  protected readonly loadError = signal(false);
  protected readonly itemsError = signal(false);

  // Marka sayfası eskiden sadece indirimli/kampanyalı ürünleri gösteriyordu
  // — normal fiyatlı ürünler tamamen görünmezdi (kategori sayfasıyla aynı
  // sorun, aynı çözüm: ana sayfadaki sekme + sayfalama deseni).
  protected readonly viewMode = signal<ViewMode>('all');
  protected readonly items = signal<Deal[]>([]);
  // bkz. deals-list.ts'teki aynı desen — kart mini-sparkline'ları için tek
  // bir toplu istek, kart başına ayrı istek değil.
  protected readonly sparklines = signal<Map<number, PricePoint[]>>(new Map());
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly currentPage = signal(1);
  protected readonly sortBy = signal<string>('');

  // Kullanıcı geri bildirimi: kategori sayfasındaki gibi bu sayfada da
  // arama/filtre yoktu. Marka zaten sabit olduğu için kategori çipleri
  // (marka çipleri değil) anlamlı bir daraltma sağlıyor.
  protected readonly searchQuery = signal('');
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  protected readonly availableCategories = signal<string[]>([]);
  protected readonly selectedCategories = signal<Set<string>>(new Set());
  protected readonly priceMin = signal<number | null>(null);
  protected readonly priceMax = signal<number | null>(null);
  protected readonly hasActiveFilters = signal(false);

  // bkz. category-page.ts'teki aynı gerekçe: ürün modalı bu sayfanın kendi
  // ?urun= query param'ına bağlı, önceden /urun/:id'ye (DealsList'in
  // route'u) navigate ediyordu — modal kapanınca marka sayfasından çıkıp
  // ana sayfaya düşen bir bug'dı.
  protected readonly selectedDeal = signal<Deal | null>(null);

  constructor() {
    effect(() => this.setFaqJsonLd());
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('brandSlug') ?? '';
      this.loadBrand(slug, params.get('categorySlug'));
    });

    this.route.queryParamMap.subscribe((params) => {
      const idParam = params.get('urun');
      if (!idParam) {
        this.selectedDeal.set(null);
        return;
      }

      const id = Number(idParam);
      const alreadyLoaded = this.items().find((d) => d.productId === id);
      if (alreadyLoaded) {
        this.selectedDeal.set(alreadyLoaded);
        return;
      }

      this.dealsService.getProductById(id).subscribe({
        next: (deal) => this.selectedDeal.set(deal),
        error: () => this.selectedDeal.set(null),
      });
    });
  }

  private loadBrand(slug: string, categorySlug: string | null): void {
    this.loading.set(true);
    this.viewMode.set('all');
    this.currentPage.set(1);
    this.searchQuery.set('');
    this.selectedCategories.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.hasActiveFilters.set(false);
    this.fixedCategory.set(null);
    this.fixedCategoryLabel.set('');
    this.brandStats.set(null);

    this.dealsService.getFilterOptions().subscribe({
      next: (options) => {
        const match = options.brands.find((b) => b.toLowerCase() === slug.toLowerCase());
        if (!match) {
          // Soft-404 yerine gerçek yönlendirme — geçersiz bir marka slug'ı
          // arama motorlarına 200 + "bulunamadı" metniyle değil, / adresine
          // yönlendirmeyle (SSR'da gerçek bir HTTP 302) dönmeli.
          this.router.navigate(['/']);
          return;
        }

        // Kesişim sayfasıysa kategori de geçerli olmalı — uydurma bir slug
        // için 200 döndürmek yerine markanın kendi sayfasına yönlendiriyoruz
        // (yukarıdaki geçersiz-marka mantığının aynısı).
        if (categorySlug) {
          if (!options.categories.includes(categorySlug)) {
            this.router.navigate(['/marka', slug.toLowerCase(), 'indirim-kodu']);
            return;
          }
          this.fixedCategory.set(categorySlug);
          this.fixedCategoryLabel.set(CATEGORY_LABELS[categorySlug] ?? categorySlug);
          this.selectedCategories.set(new Set([categorySlug]));
        }

        this.brandName.set(match);
        // Marka sayfaları birbirine link vermiyordu — diğer marka sayfalarına
        // iç linkleme için mevcut marka çıkarılmış listeyi ayrıca tutuyoruz.
        this.otherBrands.set(options.brands.filter((b) => b !== match));
        this.availableCategories.set(options.categories);
        this.setMeta(match);

        // Bu markanın gerçekten ürünü olan kategoriler — kesişim sayfalarına
        // iç linkler buradan kuruluyor, boş kombinasyona link verilmiyor.
        this.dealsService.getBrandCategoryPairs().subscribe({
          next: (pairs) => {
            this.brandCategories.set(
              pairs
                .filter((p) => p.brandName === match && p.category !== this.fixedCategory())
                .map((p) => ({ slug: p.category, label: CATEGORY_LABELS[p.category] ?? p.category, count: p.productCount })),
            );
          },
          error: () => this.brandCategories.set([]),
        });

        this.couponsService.getCoupons().subscribe((coupons) => {
          this.coupons.set(coupons.filter((c) => c.brandName === match));
        });

        // Kesişim sayfasında (categorySlug doluyken) gösterilmiyor — orada
        // zaten dar bir kategori odağı var, marka geneli istatistik yanıltıcı
        // olurdu.
        if (!categorySlug) {
          this.dealsService.getBrandStats(match).subscribe({
            next: (stats) => this.brandStats.set(stats),
            error: () => this.brandStats.set(null),
          });
        }

        this.loadItems();
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadItems(): void {
    const brand = this.brandName();
    if (!brand) return;

    this.loading.set(true);
    this.itemsError.set(false);
    this.hasActiveFilters.set(
      this.selectedCategories().size > 0 || this.priceMin() !== null || this.priceMax() !== null || !!this.searchQuery().trim(),
    );
    const query = {
      brands: [brand],
      categories: [...this.selectedCategories()],
      search: this.searchQuery().trim() || undefined,
      minPrice: this.priceMin(),
      maxPrice: this.priceMax(),
      page: this.currentPage(),
      pageSize: PAGE_SIZE,
      sortBy: this.sortBy() || undefined,
    };
    const request$ =
      this.viewMode() === 'deals'
        ? this.dealsService.getDeals(query)
        : this.viewMode() === 'store'
          ? this.dealsService.getStoreDeals(query)
          : this.dealsService.getAllProducts(query);

    request$.subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages);
        this.loading.set(false);
        this.loadSparklines(result.items);
      },
      error: () => {
        this.itemsError.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadSparklines(items: Deal[]): void {
    this.sparklines.set(new Map());
    const ids = items.map((d) => d.productId);
    this.dealsService.getSparklines(ids).subscribe((result) => {
      this.sparklines.set(new Map(result.map((s) => [s.productId, s.points])));
    });
  }

  protected sparklineFor(productId: number): PricePoint[] {
    return this.sparklines().get(productId) ?? [];
  }

  protected setViewMode(mode: ViewMode): void {
    if (this.viewMode() === mode) return;
    this.viewMode.set(mode);
    this.currentPage.set(1);
    this.loadItems();
  }

  protected onSortChange(value: string): void {
    this.sortBy.set(value);
    this.currentPage.set(1);
    this.loadItems();
  }

  protected onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.currentPage.set(1);
      this.loadItems();
    }, SEARCH_DEBOUNCE_MS);
  }

  protected toggleCategory(category: string): void {
    const current = new Set(this.selectedCategories());
    current.has(category) ? current.delete(category) : current.add(category);
    this.selectedCategories.set(current);
    this.currentPage.set(1);
    this.loadItems();
  }

  protected onPriceMinChange(value: number | null): void {
    this.priceMin.set(value);
    this.currentPage.set(1);
    this.loadItems();
  }

  protected onPriceMaxChange(value: number | null): void {
    this.priceMax.set(value);
    this.currentPage.set(1);
    this.loadItems();
  }

  protected clearFilters(): void {
    this.selectedCategories.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.searchQuery.set('');
    this.currentPage.set(1);
    this.loadItems();
  }

  protected categoryLabel(slug: string): string {
    return CATEGORY_LABELS[slug] ?? slug;
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadItems();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  private setMeta(brand: string): void {
    const category = this.fixedCategory();
    const brandSlug = brand.toLowerCase();

    // Kesişim sayfası ("Hardline Protein Tozu Fiyatları") ile marka indirim
    // kodu sayfası tamamen farklı arama niyetlerini hedefliyor — başlık,
    // açıklama ve canonical ayrı.
    if (category) {
      const label = this.fixedCategoryLabel();
      this.pageMeta.set({
        title: `${brand} ${label} Fiyatları ve İndirimleri 2026 | ProteinAvcısı`,
        description: `${brand} markasının ${label.toLocaleLowerCase('tr')} ürünleri, güncel fiyatları ve gerçek fiyat geçmişine dayanan doğrulanmış indirimleri tek sayfada.`,
        canonicalPath: `/marka/${brandSlug}/${category}`,
      });
      this.breadcrumbEl = upsertJsonLdScript(
        this.document,
        this.breadcrumbEl,
        buildBreadcrumbJsonLd(this.document, [
          { name: 'Ana Sayfa', path: '/' },
          { name: brand, path: `/marka/${brandSlug}/indirim-kodu` },
          { name: label, path: `/marka/${brandSlug}/${category}` },
        ]),
      );
      return;
    }

    const title = `${brand} İndirim Kodu ve Kampanyaları 2026 | ProteinAvcısı`;
    const description = `${brand} için güncel kupon kodları ve gerçek fiyat geçmişine dayanan doğrulanmış indirimler. ProteinAvcısı, ${brand} markasının fiyatlarını düzenli olarak takip ediyor.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/marka/${brandSlug}/indirim-kodu`,
    });
    this.breadcrumbEl = upsertJsonLdScript(
      this.document,
      this.breadcrumbEl,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: brand, path: `/marka/${brandSlug}/indirim-kodu` },
      ]),
    );
  }

  // Görünür SSS ile aynı içerikten üretiliyor — kategori sayfalarındaki desenin
  // aynısı. İkisinin ayrışmaması önemli: arama motoruna gösterilen soru/cevap,
  // sayfada gerçekten okunabilir olmalı.
  //
  // effect ile bağlı, çünkü sorular kuponlara ve marka istatistiklerine
  // dayanıyor ve ikisi de sayfa meta'sı yazıldıktan SONRA geliyor; tek seferlik
  // bir çağrı henüz boş olan veriyle şema üretirdi.
  private setFaqJsonLd(): void {
    const faqs = this.brandFaqs();
    if (faqs.length === 0) {
      // Marka × kategori sayfasına geçildiğinde önceki şema geride kalmasın.
      this.faqEl?.remove();
      this.faqEl = null;
      return;
    }

    this.faqEl = upsertJsonLdScript(this.document, this.faqEl, {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: faqs.map((faq) => ({
        '@type': 'Question',
        name: faq.question,
        acceptedAnswer: { '@type': 'Answer', text: faq.answer },
      })),
    });
  }

  protected discountBadge(deal: Deal): string {
    return `-%${deal.discountPercent}`;
  }

  protected storeDiscountBadge(deal: Deal): string {
    return `Mağaza -%${deal.storeDiscountPercent}`;
  }

  // Kart/satır bağlantıları gerçek <a href> olmak zorunda (bkz.
  // core/product-link.ts). Bu sayfalarda modal, ürün sayfasına gitmeden
  // ?urun= parametresiyle açılıyor — bu yüzden RouterLink yerine gerçek bir
  // href + kontrollü tıklama kullanılıyor: bot kanonik ürün adresini görüyor,
  // kullanıcı ise sayfadan ayrılmadan modalı açıyor.
  protected productPath(deal: Deal): string {
    return productPath(deal);
  }

  protected onProductClick(event: MouseEvent, deal: Deal): void {
    // Satırın/kartın kendi tıklama işleyicisi de varsa iki kez tetiklenmesin.
    event.stopPropagation();
    if (!shouldHandleInApp(event)) return;
    event.preventDefault();
    this.openDeal(deal);
  }

  protected openDeal(deal: Deal): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { urun: deal.productId }, queryParamsHandling: 'merge' });
  }

  protected closeDeal(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { urun: null }, queryParamsHandling: 'merge' });
  }

  protected lastCheckedText(deal: Deal): string {
    return formatRelativeTime(deal.scrapedAt);
  }

  protected goToStoreUrl(productId: number): string {
    return this.priceHistoryService.goToStoreUrl(productId);
  }

  // deals-list.ts'teki aynı yöntem — sadece gerçek besin değeri verisi
  // olan ürünlerde gösteriliyor, tahmini rakam uydurulmuyor.
  protected pricePerServing(deal: Deal): number | null {
    if (!deal.servingSizeGrams || deal.servingSizeGrams <= 0 || !deal.size) return null;
    const match = /^(\d+(?:[.,]\d+)?)\s*Gr$/i.exec(deal.size.trim());
    if (!match) return null;
    const packageGrams = Number(match[1].replace(',', '.'));
    if (!packageGrams) return null;
    const servings = packageGrams / deal.servingSizeGrams;
    if (!servings) return null;
    return deal.currentPrice / servings;
  }

  // Alfabetik sıralama ile tek bir kanonik URL üretiyoruz (hiq-vs-ssn hep
  // aynı sırada) — aksi halde aynı içeriğe iki farklı URL'den erişilebilir
  // olurdu (duplicate content riski).
  protected comparisonPairSlug(otherBrand: string): string {
    const current = this.brandName().toLowerCase();
    const other = otherBrand.toLowerCase();
    return [current, other].sort().join('-vs-');
  }
}

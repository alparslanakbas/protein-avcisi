import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { canonicalOrigin } from '../core/canonical-link';
import { CATEGORY_FAQS, FaqItem } from '../core/category-faqs';
import { CATEGORY_GUIDES, CategoryGuide } from '../core/category-guides';
import { CATEGORY_INTROS, CATEGORY_LABELS } from '../core/category-labels';
import { Deal } from '../core/deal.model';
import { productPath, shouldHandleInApp } from '../core/product-link';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { ProductModal } from '../product-modal/product-modal';
import { SiteHeader } from '../site-header/site-header';

type ViewMode = 'deals' | 'store' | 'all';
const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 350;

@Component({
  selector: 'app-category-page',
  imports: [DecimalPipe, RouterLink, FormsModule, ProductModal, SiteHeader],
  templateUrl: './category-page.html',
})
export class CategoryPage implements OnInit {
  protected readonly displayName = displayName;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private readonly priceHistoryService = inject(PriceHistoryService);

  protected readonly categorySlug = signal<string>('');
  protected readonly categoryLabel = signal<string>('');
  protected readonly categoryIntro = signal<string>('');

  // Kategoriye ÖZEL sorular — ana sayfadaki SSS platformu anlatıyor,
  // buradakiler ürünün kendisiyle ilgili ve gerçek arama ifadelerini
  // hedefliyor (bkz. category-faqs.ts). Sayfa "sadece ürün listesi"
  // olmaktan çıkıyor.
  protected readonly faqItems = signal<FaqItem[]>([]);
  private faqStructuredDataEl: HTMLScriptElement | null = null;
  private breadcrumbEl: HTMLScriptElement | null = null;
  // Uzun-format bilimsel rehber içeriği — şimdilik yalnızca en yüksek
  // hacimli 3 kategoride (bkz. core/category-guides.ts), diğerlerinde
  // null: sayfa o durumda bu bölümü hiç göstermiyor.
  protected readonly categoryGuide = signal<CategoryGuide | null>(null);
  private speakableEl: HTMLScriptElement | null = null;
  protected readonly otherCategories = signal<{ slug: string; label: string }[]>([]);
  protected readonly loading = signal(true);
  // bkz. brand-page.ts'teki aynı isim/gerekçe: bu yalnızca /api/filters
  // isteği başarısız olunca set ediliyor, geçersiz slug zaten yönlendirmeyle
  // ele alınıyor — "notFound" ismi yanıltıcıydı.
  protected readonly loadError = signal(false);
  protected readonly itemsError = signal(false);

  // Kategori sayfası eskiden sadece indirimli/kampanyalı ürünleri gösteriyordu
  // — normal fiyatlı ürünler tamamen görünmezdi. Ana sayfadaki aynı sekme +
  // sayfalama deseni buraya da taşındı ki kategori bazında TÜM ürünler de
  // görülebilsin (kullanıcı geri bildirimi: "kreatin kategorisinde sadece
  // kampanyalı ürünler geliyor").
  protected readonly viewMode = signal<ViewMode>('all');
  protected readonly items = signal<Deal[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly currentPage = signal(1);
  protected readonly sortBy = signal<string>('');
  // Kullanıcı geri bildirimi: kategori sayfasında (ana sayfanın aksine)
  // arama kutusu hiç yoktu — bir kategori içinde ürün ismine göre daraltmak
  // mümkün değildi. Backend zaten `search` parametresini destekliyordu
  // (deals-list.ts'teki aynı sorgu), sadece bu sayfaya hiç bağlanmamıştı.
  protected readonly searchQuery = signal('');
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  // Aynı gerekçeyle: marka filtresi ve fiyat aralığı da yoktu. Kategori
  // zaten bu sayfada sabit olduğu için marka çipleri anlamlı bir daraltma
  // sağlıyor (ana sayfadaki marka çipleriyle aynı desen).
  protected readonly availableBrands = signal<string[]>([]);
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  protected readonly priceMin = signal<number | null>(null);
  protected readonly priceMax = signal<number | null>(null);
  protected readonly hasActiveFilters = signal(false);

  // Ürün modalı — deals-list.ts'teki aynı desen ama path param yerine
  // ?urun= query param'ı kullanıyor (bu sayfanın kendi kanonik URL'i
  // /kategori/:slug, /urun/:id ayrı bir SAYFA'ya değil, aynı sayfa
  // üzerinde bir modal durumuna işaret etmeli). Daha önce goToProduct
  // doğrudan /urun/:id'ye (DealsList'in route'u) navigate ediyordu — bu
  // kategori sayfasından TAMAMEN ayrılıp modal kapanınca ana sayfaya
  // dönmesine yol açan gerçek bir bug'dı (kullanıcı bildirdi).
  protected readonly selectedDeal = signal<Deal | null>(null);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('categorySlug') ?? '';
      this.loadCategory(slug);
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

  private loadCategory(slug: string): void {
    this.loading.set(true);
    this.viewMode.set('all');
    this.currentPage.set(1);
    this.searchQuery.set('');
    this.selectedBrands.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.hasActiveFilters.set(false);

    this.dealsService.getFilterOptions().subscribe({
      next: (options) => {
        const match = options.categories.find((c) => c.toLowerCase() === slug.toLowerCase());
        const label = match ? CATEGORY_LABELS[match] : undefined;
        if (!match || !label) {
          // Soft-404 yerine gerçek yönlendirme (bkz. marka sayfalarındaki aynı karar).
          this.router.navigate(['/']);
          return;
        }

        this.categorySlug.set(match);
        this.categoryLabel.set(label);
        this.categoryIntro.set(CATEGORY_INTROS[match]);
        this.categoryGuide.set(CATEGORY_GUIDES[match] ?? null);
        this.setFaq(match);
        this.otherCategories.set(
          options.categories
            .filter((c) => c !== match)
            .map((c) => ({ slug: c, label: CATEGORY_LABELS[c] ?? c })),
        );
        this.availableBrands.set(options.brands);
        this.setMeta(label, match);

        this.loadItems();
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadItems(): void {
    const categorySlug = this.categorySlug();
    if (!categorySlug) return;

    this.loading.set(true);
    this.itemsError.set(false);
    this.hasActiveFilters.set(
      this.selectedBrands().size > 0 || this.priceMin() !== null || this.priceMax() !== null || !!this.searchQuery().trim(),
    );
    const query = {
      categories: [categorySlug],
      brands: [...this.selectedBrands()],
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
      },
      error: () => {
        this.itemsError.set(true);
        this.loading.set(false);
      },
    });
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

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    current.has(brand) ? current.delete(brand) : current.add(brand);
    this.selectedBrands.set(current);
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
    this.selectedBrands.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.searchQuery.set('');
    this.currentPage.set(1);
    this.loadItems();
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadItems();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // Görünür SSS + aynı içerikten üretilen FAQPage structured data.
  // Google bu işaretlemeyi arama sonucunda açılır soru-cevap olarak
  // gösterebiliyor (ana sayfadaki aynı desen, deals-list.ts).
  private setFaq(slug: string): void {
    const items = CATEGORY_FAQS[slug] ?? [];
    this.faqItems.set(items);

    if (items.length === 0) {
      this.faqStructuredDataEl?.remove();
      this.faqStructuredDataEl = null;
      return;
    }

    this.faqStructuredDataEl = upsertJsonLdScript(this.document, this.faqStructuredDataEl, {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: items.map((item) => ({
        '@type': 'Question',
        name: item.question,
        acceptedAnswer: { '@type': 'Answer', text: item.answer },
      })),
    });
  }

  private setMeta(label: string, slug: string): void {
    const title = `${label} Fiyatları ve İndirimleri 2026 | ProteinAvcısı`;
    const description = `${label} kategorisindeki güncel fiyatlar, gerçek fiyat geçmişine dayanan doğrulanmış indirimler ve mağaza kampanyaları. ProteinAvcısı, fiyatları düzenli olarak takip ediyor.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/kategori/${slug}`,
    });

    this.breadcrumbEl = upsertJsonLdScript(
      this.document,
      this.breadcrumbEl,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Kategoriler', path: '/kategoriler' },
        { name: label, path: `/kategori/${slug}` },
      ]),
    );

    // Uzun-format rehber içeriği olan kategorilerde, AI/sesli asistan
    // motorlarının doğrudan alıntılayabileceği bir "konuşulabilir" bölüm
    // işaretlemesi (rakip analizinde gördüğümüz bir desen).
    if (CATEGORY_GUIDES[slug]) {
      this.speakableEl = upsertJsonLdScript(this.document, this.speakableEl, {
        '@context': 'https://schema.org',
        '@type': 'WebPage',
        speakable: { '@type': 'SpeakableSpecification', cssSelector: ['#zero-click-answer'] },
        url: `${canonicalOrigin(this.document)}/kategori/${slug}`,
      });
    } else {
      this.speakableEl?.remove();
      this.speakableEl = null;
    }
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

  protected goToStoreUrl(deal: Deal): string {
    return this.priceHistoryService.goToStoreUrl(deal.productId, deal.storeUrl);
  }

  /** Mağaza tıklamasını sayar; bağlantı doğrudan mağazaya gittiği için
   *  sayacı artık /go/{id} artıramıyor (bkz. PriceHistoryService). */
  protected magazaTiklamasi(productId: number): void {
    this.priceHistoryService.trackStoreClick(productId);
  }
}

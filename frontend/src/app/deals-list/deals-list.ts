import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Coupon } from '../core/coupon.model';
import { CouponsService } from '../core/coupons.service';
import { Deal } from '../core/deal.model';
import { DealsQuery, DealsService } from '../core/deals.service';
import { formatRelativeTime } from '../core/relative-time';
import { ThemePreference, ThemeService } from '../core/theme.service';
import { ProductModal } from '../product-modal/product-modal';
import { ShareButton } from '../share-button/share-button';

type ViewMode = 'deals' | 'all' | 'store';

const isMac = typeof navigator !== 'undefined' && /Mac|iPod|iPhone|iPad/.test(navigator.platform);
const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 350;

// Title/description'da bilinçli olarak "Protein Avcısı" (boşluklu) kullanılıyor
// — logodaki bitişik "ProteinAvcısı" yazımı marka kimliği olarak kalıyor, ama
// insanlar arama kutusuna doğal olarak boşluklu yazıyor; arama motoruna dönük
// metinlerde bu ayrımı güçlendirmek ucuz ve düşük riskli bir SEO düzeltmesi.
const DEFAULT_TITLE = 'Protein Avcısı | Güncel İndirim ve Kampanyalar — Spor Takviyesi Fiyat Takibi';
const DEFAULT_DESCRIPTION =
  'Protein Avcısı; protein tozu, kreatin, pre-workout ve diğer spor takviyelerinde markanın beyanına değil, gerçek fiyat geçmişine dayanan doğrulanmış indirimleri gösterir. HIQ, SSN, Hardline ve ProteinOcean tek yerde.';

// Tasarım güncellemesi (bir geliştiricinin "daha fazla içerik/güven
// unsuru" geri bildirimi üzerine): gerçek, uydurma olmayan sorular —
// hepsi zaten sitede var olan davranışları açıklıyor, pazarlama amaçlı
// abartı yok. Aynı zamanda FAQPage structured data için de kullanılıyor.
const FAQ_ITEMS: { question: string; answer: string }[] = [
  {
    question: '"İndirimdekiler" ile "Mağaza Kampanyaları" arasındaki fark nedir?',
    answer:
      '"İndirimdekiler", bizim topladığımız gerçek fiyat geçmişine dayanır — bir ürünün güncel fiyatı son 30 günün en yüksek fiyatından düşükse burada listelenir. "Mağaza Kampanyaları" ise markanın kendi sitesinde beyan ettiği eski/yeni fiyat farkıdır, henüz bizim tarafımızdan doğrulanmamıştır.',
  },
  {
    question: 'Fiyatlar ne sıklıkla güncelleniyor?',
    answer: 'Takip ettiğimiz markalar günde 4 kez otomatik olarak taranıyor, fiyat değişiklikleri buna göre güncelleniyor.',
  },
  {
    question: 'Ürünü ProteinAvcısı üzerinden mi satın alıyorum?',
    answer:
      'Hayır. ProteinAvcısı bir satış sitesi değil, fiyat takip sitesidir. "Mağazaya Git" butonuna tıklayınca doğrudan ilgili markanın kendi sitesine yönlendirilirsin, satış işlemi orada gerçekleşir.',
  },
  {
    question: 'Kupon kodlarını nereden buluyorsunuz?',
    answer:
      'Kupon kodları otomatik toplanmıyor — süresi geçmiş ya da hatalı bir kod göstermemek için yalnızca elle doğruladığımız kodları yayınlıyoruz.',
  },
  {
    question: 'Neden bazı ürünlerde "servis başı fiyat" gösterilmiyor?',
    answer:
      'Bu bilgiyi yalnızca markanın gerçek besin değeri tablosuna ulaşabildiğimiz ürünlerde gösteriyoruz; tahmini bir rakam paylaşmıyoruz.',
  },
];

@Component({
  selector: 'app-deals-list',
  imports: [DecimalPipe, FormsModule, ProductModal, ShareButton, RouterLink],
  templateUrl: './deals-list.html',
})
export class DealsList implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly couponsService = inject(CouponsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);
  protected readonly theme = inject(ThemeService);
  private readonly searchInput = viewChild<{ nativeElement: HTMLInputElement }>('searchInput');
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly shortcutLabel = isMac ? '⌘K' : 'Ctrl+K';

  protected readonly deals = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  // Varsayılan sekme bilinçli olarak "store": "İndirimdekiler" (kendi
  // doğruladığımız indirim) şu aşamada çoğunlukla boş geliyor — yeni bir
  // ziyaretçinin ilk gördüğü şey boş bir sayfa olunca güven kırıcı oluyor
  // (kullanıcı testinde gerçek geri bildirim). "Mağaza Kampanyaları" hep
  // dolu, dürüstçe "doğrulanmamış" etiketli ama boş görünmüyor.
  protected readonly viewMode = signal<ViewMode>('store');
  protected readonly selectedDeal = signal<Deal | null>(null);

  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly currentPage = signal(1);

  protected readonly searchQuery = signal('');
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  protected readonly selectedCategories = signal<Set<string>>(new Set());
  protected readonly priceMin = signal<number | null>(null);
  protected readonly priceMax = signal<number | null>(null);
  protected readonly sortBy = signal<string>('');

  protected readonly availableBrands = signal<string[]>([]);
  protected readonly availableCategories = signal<string[]>([]);

  protected readonly hasActiveFilters = signal(false);

  protected readonly coupons = signal<Coupon[]>([]);

  // Sayfa aşağı kaydırılınca sağ altta çıkan "yukarı çık" butonu için.
  protected readonly showScrollTop = signal(false);

  // Ana sayfadaki gerçek istatistik şeridi için — filtreden bağımsız,
  // tüm katalog sayısı (mevcut sekme/filtreye göre değişen totalCount()'tan
  // ayrı). Uydurma bir rakam değil, /api/products'tan gelen gerçek toplam.
  protected readonly siteProductCount = signal(0);
  protected readonly faqItems = FAQ_ITEMS;

  constructor() {
    // Ürün modalı açıkken title/description/Open Graph o ürüne özel oluyor
    // (SSR ile birleşince /urun/:id linki paylaşılınca veya Google'da
    // gerçek ürün bilgisiyle görünüyor); kapanınca site geneline dönüyor.
    effect(() => {
      const deal = this.selectedDeal();

      if (!deal) {
        this.titleService.setTitle(DEFAULT_TITLE);
        this.metaService.updateTag({ name: 'description', content: DEFAULT_DESCRIPTION });
        this.metaService.updateTag({ property: 'og:title', content: DEFAULT_TITLE });
        this.metaService.updateTag({ property: 'og:description', content: DEFAULT_DESCRIPTION });
        this.metaService.updateTag({ property: 'og:type', content: 'website' });
        this.metaService.removeTag('property="og:image"');
        this.structuredDataEl?.remove();
        this.structuredDataEl = null;
        return;
      }

      const priceText = `${deal.currentPrice.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`;
      const title = `${deal.productName} Fiyatı: ${priceText} | ${deal.brandName} — ProteinAvcısı`;
      const description =
        deal.discountPercent > 0
          ? `${deal.productName} şu an ${priceText} — ${deal.brandName} markasında %${deal.discountPercent} doğrulanmış indirim. Fiyat geçmişini ProteinAvcısı'nda takip et.`
          : `${deal.productName} güncel fiyatı ${priceText}. ${deal.brandName} markasının fiyat geçmişini ProteinAvcısı'nda takip et.`;

      this.titleService.setTitle(title);
      this.metaService.updateTag({ name: 'description', content: description });
      this.metaService.updateTag({ property: 'og:title', content: title });
      this.metaService.updateTag({ property: 'og:description', content: description });
      this.metaService.updateTag({ property: 'og:type', content: 'product' });
      if (deal.imageUrl) {
        this.metaService.updateTag({ property: 'og:image', content: deal.imageUrl });
      } else {
        this.metaService.removeTag('property="og:image"');
      }

      // schema.org Product/Offer — Google'ın arama sonucunda fiyat gösterme
      // ihtimali için. "availability" bilinçli olarak yok: 4 markanın
      // hepsinde güvenilir stok bilgisi çekmiyoruz (SSN/Hardline stok
      // durumunu hiç kontrol etmiyor), olmayan veriyi "InStock" diye
      // iddia etmektense alanı hiç eklememeyi tercih ettik.
      const jsonLd = {
        '@context': 'https://schema.org',
        '@type': 'Product',
        name: deal.productName,
        sku: String(deal.productId),
        ...(deal.imageUrl ? { image: deal.imageUrl } : {}),
        brand: { '@type': 'Brand', name: deal.brandName },
        offers: {
          '@type': 'Offer',
          url: `${this.document.location.origin}/urun/${deal.productId}`,
          priceCurrency: 'TRY',
          price: deal.currentPrice.toFixed(2),
        },
      };

      if (!this.structuredDataEl) {
        this.structuredDataEl = this.document.createElement('script');
        this.structuredDataEl.type = 'application/ld+json';
        this.document.head.appendChild(this.structuredDataEl);
      }
      this.structuredDataEl.textContent = JSON.stringify(jsonLd);
    });
  }

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.availableBrands.set(options.brands);
      this.availableCategories.set(options.categories);
    });
    this.couponsService.getCoupons().subscribe((coupons) => this.coupons.set(coupons));
    // pageSize:1 — sadece toplam sayıyı okumak için, tüm ürünleri çekmeye gerek yok.
    this.dealsService.getAllProducts({ pageSize: 1 }).subscribe((result) => this.siteProductCount.set(result.totalCount));
    this.addFaqStructuredData();
    this.load();

    // Sayfa numarası URL'de ?page= olarak tutuluyor — tarayıcının geri/ileri
    // (mouse yan tuşları dahil) butonlarının sayfalama geçmişinde doğru
    // gezinebilmesi için. goToPage() zaten currentPage'i set edip load()
    // çağırdığından, bu abonelik asıl olarak URL DIŞARIDAN değiştiğinde
    // (geri/ileri tuşu, paylaşılan link) devreye giriyor — sayfa zaten
    // eşleşiyorsa tekrar yüklemiyor.
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const page = Math.max(1, Number(params.get('page')) || 1);
      if (page !== this.currentPage()) {
        this.currentPage.set(page);
        this.load();
      }
    });

    // Ürün modalı artık URL'e bağlı (/urun/:id) — bileşen '' ve 'urun/:id'
    // arasında yeniden kurulmadan yaşadığı için (bkz. DealsRouteReuseStrategy)
    // parametre değişikliklerine burada tek seferlik abone oluyoruz.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const idParam = params.get('id');
      if (!idParam) {
        this.selectedDeal.set(null);
        return;
      }

      const id = Number(idParam);
      const alreadyLoaded = this.deals().find((d) => d.productId === id);
      if (alreadyLoaded) {
        this.selectedDeal.set(alreadyLoaded);
        return;
      }

      this.dealsService.getProductById(id).subscribe({
        next: (deal) => this.selectedDeal.set(deal),
        error: () => this.router.navigate(['/']),
      });
    });
  }

  // FAQ_ITEMS statik olduğu için (ürün modalındaki gibi değişmiyor) tek
  // seferlik ekleniyor, ürün modalının açılıp kapanmasıyla ayrı bir
  // <script> etiketi olarak kalıyor.
  private addFaqStructuredData(): void {
    const jsonLd = {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: FAQ_ITEMS.map((item) => ({
        '@type': 'Question',
        name: item.question,
        acceptedAnswer: { '@type': 'Answer', text: item.answer },
      })),
    };

    const script = this.document.createElement('script');
    script.type = 'application/ld+json';
    script.textContent = JSON.stringify(jsonLd);
    this.document.head.appendChild(script);
  }

  protected setViewMode(mode: ViewMode): void {
    if (this.viewMode() === mode) return;
    this.viewMode.set(mode);
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.currentPage.set(1);
      this.load();
      this.syncPageQueryParam(1, false);
    }, SEARCH_DEBOUNCE_MS);
  }

  protected onSortChange(value: string): void {
    this.sortBy.set(value);
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    current.has(brand) ? current.delete(brand) : current.add(brand);
    this.selectedBrands.set(current);
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected toggleCategory(category: string): void {
    const current = new Set(this.selectedCategories());
    current.has(category) ? current.delete(category) : current.add(category);
    this.selectedCategories.set(current);
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected onPriceMinChange(value: number | null): void {
    this.priceMin.set(value);
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected onPriceMaxChange(value: number | null): void {
    this.priceMax.set(value);
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected clearFilters(): void {
    this.selectedBrands.set(new Set());
    this.selectedCategories.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.searchQuery.set('');
    this.currentPage.set(1);
    this.load();
    this.syncPageQueryParam(1, false);
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.load();
    // Yeni bir tarayıcı geçmişi kaydı oluştur (push) — geri/ileri tuşlarıyla
    // sayfalar arasında doğru gezinilsin diye. Filtre/arama/sıralama
    // değişiklikleri bunun aksine replaceUrl kullanıyor (bkz. syncPageQueryParam
    // çağrıları yukarıda) — her filtre tıklaması ayrı bir "geri" durağı
    // olmasın diye.
    this.syncPageQueryParam(page, true);
    // Sayfa değişince en üste dön, kullanıcı grid'in ortasında kalmasın.
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  private syncPageQueryParam(page: number, push: boolean): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page > 1 ? page : null },
      queryParamsHandling: 'merge',
      replaceUrl: !push,
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    const query: DealsQuery = {
      brands: [...this.selectedBrands()],
      categories: [...this.selectedCategories()],
      search: this.searchQuery().trim() || undefined,
      minPrice: this.priceMin(),
      maxPrice: this.priceMax(),
      sortBy: this.sortBy() || undefined,
      page: this.currentPage(),
      pageSize: PAGE_SIZE,
    };

    this.hasActiveFilters.set(
      query.brands!.length > 0 ||
        query.categories!.length > 0 ||
        this.priceMin() !== null ||
        this.priceMax() !== null ||
        !!query.search,
    );

    const request$ =
      this.viewMode() === 'deals'
        ? this.dealsService.getDeals(query)
        : this.viewMode() === 'store'
          ? this.dealsService.getStoreDeals(query)
          : this.dealsService.getAllProducts(query);

    request$.subscribe({
      next: (result) => {
        this.deals.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Veriler yüklenemedi. API çalışıyor mu kontrol et.');
        this.loading.set(false);
      },
    });
  }

  protected discountBadge(deal: Deal): string {
    return `-%${deal.discountPercent}`;
  }

  protected storeDiscountBadge(deal: Deal): string {
    return `Mağaza -%${deal.storeDiscountPercent}`;
  }

  protected lastCheckedText(deal: Deal): string {
    return formatRelativeTime(deal.scrapedAt);
  }

  protected categoryLabel(category: string): string {
    return category
      .split('-')
      .map((word) => word.charAt(0).toLocaleUpperCase('tr') + word.slice(1))
      .join(' ');
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
  }

  protected openDeal(deal: Deal): void {
    this.router.navigate(['/urun', deal.productId]);
  }

  protected closeDeal(): void {
    this.router.navigate(['/']);
  }

  // Gerçek besin değeri verisi olan ürünlerde (şimdilik sadece HIQ) servis
  // başı fiyat gösteriyoruz. Sadece paket boyutu gram cinsindeyse hesaplıyoruz
  // — "adet/kapsül" gibi birimlerde gram varsayımı yanlış olur, o yüzden
  // uydurmak yerine null dönüp göstermiyoruz.
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

  @HostListener('document:keydown', ['$event'])
  protected onGlobalKeydown(event: KeyboardEvent): void {
    const isShortcut = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
    if (!isShortcut) return;

    event.preventDefault();
    this.searchInput()?.nativeElement.focus();
  }

  @HostListener('window:scroll')
  protected onWindowScroll(): void {
    this.showScrollTop.set(window.scrollY > 400);
  }

  protected scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}

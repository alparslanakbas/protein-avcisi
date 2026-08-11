import { DecimalPipe } from '@angular/common';
import { Component, HostListener, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Coupon } from '../core/coupon.model';
import { CouponsService } from '../core/coupons.service';
import { Deal } from '../core/deal.model';
import { DealsQuery, DealsService } from '../core/deals.service';
import { ThemePreference, ThemeService } from '../core/theme.service';
import { ProductModal } from '../product-modal/product-modal';

type ViewMode = 'deals' | 'all' | 'store';

const isMac = /Mac|iPod|iPhone|iPad/.test(navigator.platform);
const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 350;

@Component({
  selector: 'app-deals-list',
  imports: [DecimalPipe, FormsModule, ProductModal],
  templateUrl: './deals-list.html',
})
export class DealsList implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly couponsService = inject(CouponsService);
  protected readonly theme = inject(ThemeService);
  private readonly searchInput = viewChild<{ nativeElement: HTMLInputElement }>('searchInput');
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  protected readonly shortcutLabel = isMac ? '⌘K' : 'Ctrl+K';

  protected readonly deals = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly viewMode = signal<ViewMode>('deals');
  protected readonly selectedDeal = signal<Deal | null>(null);

  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly currentPage = signal(1);

  protected readonly searchQuery = signal('');
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  protected readonly selectedCategories = signal<Set<string>>(new Set());
  protected readonly priceMin = signal<number | null>(null);
  protected readonly priceMax = signal<number | null>(null);

  protected readonly availableBrands = signal<string[]>([]);
  protected readonly availableCategories = signal<string[]>([]);

  protected readonly hasActiveFilters = signal(false);

  protected readonly coupons = signal<Coupon[]>([]);

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.availableBrands.set(options.brands);
      this.availableCategories.set(options.categories);
    });
    this.couponsService.getCoupons().subscribe((coupons) => this.coupons.set(coupons));
    this.load();
  }

  protected setViewMode(mode: ViewMode): void {
    if (this.viewMode() === mode) return;
    this.viewMode.set(mode);
    this.currentPage.set(1);
    this.load();
  }

  protected onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.currentPage.set(1);
      this.load();
    }, SEARCH_DEBOUNCE_MS);
  }

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    current.has(brand) ? current.delete(brand) : current.add(brand);
    this.selectedBrands.set(current);
    this.currentPage.set(1);
    this.load();
  }

  protected toggleCategory(category: string): void {
    const current = new Set(this.selectedCategories());
    current.has(category) ? current.delete(category) : current.add(category);
    this.selectedCategories.set(current);
    this.currentPage.set(1);
    this.load();
  }

  protected onPriceMinChange(value: number | null): void {
    this.priceMin.set(value);
    this.currentPage.set(1);
    this.load();
  }

  protected onPriceMaxChange(value: number | null): void {
    this.priceMax.set(value);
    this.currentPage.set(1);
    this.load();
  }

  protected clearFilters(): void {
    this.selectedBrands.set(new Set());
    this.selectedCategories.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.searchQuery.set('');
    this.currentPage.set(1);
    this.load();
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.load();
    // Sayfa değişince en üste dön, kullanıcı grid'in ortasında kalmasın.
    window.scrollTo({ top: 0, behavior: 'smooth' });
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
    this.selectedDeal.set(deal);
  }

  protected closeDeal(): void {
    this.selectedDeal.set(null);
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
}

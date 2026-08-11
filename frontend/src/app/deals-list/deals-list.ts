import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { ThemePreference, ThemeService } from '../core/theme.service';

type ViewMode = 'deals' | 'all';

@Component({
  selector: 'app-deals-list',
  imports: [DecimalPipe, FormsModule],
  templateUrl: './deals-list.html',
})
export class DealsList implements OnInit {
  private readonly dealsService = inject(DealsService);
  protected readonly theme = inject(ThemeService);

  protected readonly deals = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly viewMode = signal<ViewMode>('deals');

  protected readonly searchQuery = signal('');
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  protected readonly selectedCategories = signal<Set<string>>(new Set());
  protected readonly priceMin = signal<number | null>(null);
  protected readonly priceMax = signal<number | null>(null);

  protected readonly availableBrands = computed(() =>
    [...new Set(this.deals().map((d) => d.brandName))].sort((a, b) => a.localeCompare(b, 'tr')),
  );

  protected readonly availableCategories = computed(() =>
    [...new Set(this.deals().map((d) => d.category).filter((c): c is string => !!c))].sort((a, b) =>
      a.localeCompare(b, 'tr'),
    ),
  );

  protected readonly filteredDeals = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase('tr');
    const brands = this.selectedBrands();
    const categories = this.selectedCategories();
    const min = this.priceMin();
    const max = this.priceMax();

    return this.deals().filter((deal) => {
      if (query) {
        // Kategori "protein-tozu" gibi tire'li geliyor; "protein tozu" araması da eşleşsin diye normalize ediyoruz.
        const haystack = [deal.productName, deal.brandName, deal.category, deal.size, deal.flavor]
          .filter(Boolean)
          .join(' ')
          .replace(/-/g, ' ')
          .toLocaleLowerCase('tr');
        if (!haystack.includes(query)) return false;
      }
      if (brands.size > 0 && !brands.has(deal.brandName)) return false;
      if (categories.size > 0 && (!deal.category || !categories.has(deal.category))) return false;
      if (min !== null && deal.currentPrice < min) return false;
      if (max !== null && deal.currentPrice > max) return false;
      return true;
    });
  });

  protected readonly hasActiveFilters = computed(
    () =>
      this.selectedBrands().size > 0 ||
      this.selectedCategories().size > 0 ||
      this.priceMin() !== null ||
      this.priceMax() !== null,
  );

  ngOnInit(): void {
    this.load();
  }

  protected setViewMode(mode: ViewMode): void {
    if (this.viewMode() === mode) return;
    this.viewMode.set(mode);
    this.load();
  }

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    if (current.has(brand)) {
      current.delete(brand);
    } else {
      current.add(brand);
    }
    this.selectedBrands.set(current);
  }

  protected toggleCategory(category: string): void {
    const current = new Set(this.selectedCategories());
    if (current.has(category)) {
      current.delete(category);
    } else {
      current.add(category);
    }
    this.selectedCategories.set(current);
  }

  protected clearFilters(): void {
    this.selectedBrands.set(new Set());
    this.selectedCategories.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
  }

  protected onPriceMinChange(value: number | null): void {
    this.priceMin.set(value);
  }

  protected onPriceMaxChange(value: number | null): void {
    this.priceMax.set(value);
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    const request$ = this.viewMode() === 'deals'
      ? this.dealsService.getDeals()
      : this.dealsService.getAllProducts();

    request$.subscribe({
      next: (deals) => {
        this.deals.set(deals);
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

  protected categoryLabel(category: string): string {
    return category
      .split('-')
      .map((word) => word.charAt(0).toLocaleUpperCase('tr') + word.slice(1))
      .join(' ');
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
  }
}

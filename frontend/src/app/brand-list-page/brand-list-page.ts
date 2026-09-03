import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import {
  brandLogoNeedsDarkBackdrop,
  brandLogoUrl,
  brandMonogram,
  brandMonogramColor,
} from '../core/brand-logo';
import { brandSlug } from '../core/brand-slug';
import { CATEGORY_LABELS } from '../core/category-labels';
import { BrandCategoryPair, BrandProductCount, DealsService } from '../core/deals.service';
import { PageMetaService } from '../core/page-meta.service';
import { normalizeSearchText } from '../core/search-normalize';
import { SiteHeader } from '../site-header/site-header';

type BrandSortMode = 'product-count' | 'name';

interface BrandCategorySummary {
  slug: string;
  label: string;
  productCount: number;
}

interface BrandDirectoryItem {
  name: string;
  slug: string;
  productCount: number;
  categories: BrandCategorySummary[];
  logoUrl: string | null;
  logoNeedsDarkBackdrop: boolean;
  monogram: string;
  monogramColor: string;
}

const PAGE_SIZE = 12;

@Component({
  selector: 'app-brand-list-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './brand-list-page.html',
  styleUrl: './brand-list-page.css',
})
export class BrandListPage implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly brands = signal<BrandDirectoryItem[]>([]);
  protected readonly searchQuery = signal('');
  protected readonly sortMode = signal<BrandSortMode>('product-count');
  protected readonly currentPage = signal(1);
  protected readonly selectedSlug = signal('');
  protected readonly failedLogos = signal<ReadonlySet<string>>(new Set());

  protected readonly filteredBrands = computed(() => {
    const query = this.normalize(this.searchQuery());
    const result = this.brands().filter((brand) => {
      if (!query) return true;
      const searchable = this.normalize(
        `${brand.name} ${brand.categories.map((category) => category.label).join(' ')}`,
      );
      return searchable.includes(query);
    });

    return [...result].sort((a, b) => {
      if (this.sortMode() === 'name') return a.name.localeCompare(b.name, 'tr-TR');
      return b.productCount - a.productCount || a.name.localeCompare(b.name, 'tr-TR');
    });
  });

  protected readonly totalPages = computed(() => Math.ceil(this.filteredBrands().length / PAGE_SIZE));

  protected readonly pageBrands = computed(() => {
    const start = (this.currentPage() - 1) * PAGE_SIZE;
    return this.filteredBrands().slice(start, start + PAGE_SIZE);
  });

  protected readonly selectedBrand = computed(() => {
    const visibleBrands = this.pageBrands();
    return visibleBrands.find((brand) => brand.slug === this.selectedSlug()) ?? visibleBrands[0] ?? null;
  });

  protected readonly visiblePages = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 5) return Array.from({ length: total }, (_, index) => index + 1);
    const start = Math.min(Math.max(1, current - 2), total - 4);
    return Array.from({ length: 5 }, (_, index) => start + index);
  });

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Spor Takviyesi Markaları ve Güncel Fiyatları | ProteinAvcısı',
      description:
        'Protein tozu, kreatin ve sporcu gıdası markalarını gerçek ürün sayıları, güncel fiyatları ve fiyat geçmişleriyle keşfet.',
      canonicalPath: '/markalar',
    });

    forkJoin({
      filters: this.dealsService.getFilterOptions(),
      pairs: this.dealsService.getBrandCategoryPairs(),
      counts: this.dealsService.getBrandProductCounts(),
    }).subscribe({
      next: ({ filters, pairs, counts }) => {
        const items = this.buildBrandItems(filters.brands, pairs, counts);
        this.brands.set(items);
        const preferred = items.find((brand) => brand.name === 'ProteinOcean') ?? items[0];
        this.selectedSlug.set(preferred?.slug ?? '');
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  protected setSearchQuery(value: string): void {
    this.searchQuery.set(value);
    this.currentPage.set(1);
    this.selectFirstVisibleBrand();
  }

  protected setSortMode(value: string): void {
    if (value !== 'product-count' && value !== 'name') return;
    this.sortMode.set(value);
    this.currentPage.set(1);
    this.selectFirstVisibleBrand();
  }

  protected selectBrand(brand: BrandDirectoryItem): void {
    this.selectedSlug.set(brand.slug);
  }

  protected goToPage(page: number): void {
    const nextPage = Math.min(Math.max(1, page), this.totalPages());
    if (!Number.isFinite(nextPage) || nextPage === this.currentPage()) return;
    this.currentPage.set(nextPage);
    this.selectFirstVisibleBrand();
  }

  protected selectedBrandDescription(brand: BrandDirectoryItem): string {
    return `${brand.name} markasının takip ettiğimiz ${brand.productCount} ürünü için güncel fiyatları, doğrulanmış indirimleri ve fiyat geçmişini tek yerde incele.`;
  }

  protected markLogoFailed(slug: string): void {
    this.failedLogos.update((current) => new Set([...current, slug]));
  }

  private buildBrandItems(
    brandNames: string[],
    pairs: BrandCategoryPair[],
    counts: BrandProductCount[],
  ): BrandDirectoryItem[] {
    const categoriesByBrand = new Map<string, Map<string, number>>();

    for (const pair of pairs) {
      const categories = categoriesByBrand.get(pair.brandName) ?? new Map<string, number>();
      categories.set(pair.category, pair.productCount);
      categoriesByBrand.set(pair.brandName, categories);
    }

    // Ürün sayısı kategori çiftlerinden TOPLANMIYOR: o liste yalnızca
    // kategorisi olan ürünleri sayıyor ve marka sayfasındaki rakamdan
    // sapıyordu (HIQ dizinde 85, kendi sayfasında 113). Kategori çiftleri
    // yalnızca kategori çipleri için kullanılıyor.
    const countByBrand = new Map(counts.map((row) => [row.brandName, row.productCount]));

    return brandNames
      .map((name) => {
        const categories = [...(categoriesByBrand.get(name)?.entries() ?? [])]
          .map(([slug, productCount]) => ({
            slug,
            label: CATEGORY_LABELS[slug] ?? slug,
            productCount,
          }))
          .sort((a, b) => b.productCount - a.productCount);

        return {
          name,
          slug: brandSlug(name),
          productCount: countByBrand.get(name) ?? 0,
          categories,
          logoUrl: brandLogoUrl(name),
          logoNeedsDarkBackdrop: brandLogoNeedsDarkBackdrop(name),
          monogram: brandMonogram(name),
          monogramColor: brandMonogramColor(name),
        };
      })
      .sort((a, b) => b.productCount - a.productCount || a.name.localeCompare(b.name, 'tr-TR'));
  }

  private selectFirstVisibleBrand(): void {
    this.selectedSlug.set(this.pageBrands()[0]?.slug ?? '');
  }

  // T\u00fcrk\u00e7e harf tuza\u011f\u0131 i\u00e7in ortak yard\u0131mc\u0131 \u2014 gerek\u00e7esi ve testleri
  // `core/search-normalize.ts` i\u00e7inde.
  private normalize(value: string): string {
    return normalizeSearchText(value);
  }
}

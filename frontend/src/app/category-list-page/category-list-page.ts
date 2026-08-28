import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { CATEGORY_INTROS, CATEGORY_LABELS } from '../core/category-labels';
import { DealsService } from '../core/deals.service';
import { PageMetaService } from '../core/page-meta.service';
import { categoryPhosphorIcon } from '../core/nav-icons';
import { SiteHeader } from '../site-header/site-header';

interface CategoryCard {
  slug: string;
  label: string;
  intro: string;
  productCount: number;
  iconClass: string;
  tone: CategoryTone;
}

type CategoryFilter = 'all' | 'performance' | 'nutrition' | 'weight';
type CategoryTone = 'violet' | 'mint' | 'blue' | 'indigo' | 'cyan' | 'rose' | 'orange';

const CATEGORY_FILTER_SLUGS: Record<Exclude<CategoryFilter, 'all'>, ReadonlySet<string>> = {
  performance: new Set(['amino-asitler', 'kreatin', 'pre-workout']),
  nutrition: new Set(['protein-tozu', 'vitamin', 'saglikli-atistirmaliklar']),
  weight: new Set(['kilo-hacim', 'l-carnitine-cla', 'yag-yakici']),
};

const CATEGORY_TONES: Record<string, CategoryTone> = {
  'protein-tozu': 'violet',
  vitamin: 'mint',
  'amino-asitler': 'blue',
  'kilo-hacim': 'indigo',
  'l-carnitine-cla': 'mint',
  kreatin: 'blue',
  'pre-workout': 'rose',
  'saglikli-atistirmaliklar': 'orange',
  'yag-yakici': 'rose',
};

// Kullanıcı geri bildirimi: kategori sayfalarına nav'daki bir dropdown'dan
// başka erişim yoktu, bu da onları neredeyse görünmez kılıyordu — bu sayfa
// tüm kategorileri tek bir yerde, gerçek ürün sayılarıyla listeleyen kalıcı
// bir indeks. Mobil alt sekme çubuğunun da "Kategoriler" hedefi burası olacak.
@Component({
  selector: 'app-category-list-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './category-list-page.html',
})
export class CategoryListPage implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly categories = signal<CategoryCard[]>([]);
  protected readonly categorySearch = signal('');
  protected readonly activeFilter = signal<CategoryFilter>('all');
  protected readonly categoryFilters: { value: CategoryFilter; label: string }[] = [
    { value: 'all', label: 'Tümü' },
    { value: 'performance', label: 'Performans' },
    { value: 'nutrition', label: 'Beslenme' },
    { value: 'weight', label: 'Kilo Kontrolü' },
  ];

  protected readonly filteredCategories = computed(() => {
    const query = this.normalizeSearch(this.categorySearch());
    const activeFilter = this.activeFilter();

    return this.categories().filter((category) => {
      const matchesFilter =
        activeFilter === 'all' || CATEGORY_FILTER_SLUGS[activeFilter].has(category.slug);
      const searchableText = this.normalizeSearch(`${category.label} ${category.intro}`);
      return matchesFilter && (!query || searchableText.includes(query));
    });
  });

  ngOnInit(): void {
    this.pageMeta.set({
      // "Tüm Kategoriler" hiçbir arama niyetiyle eşleşmiyordu — konuyu
      // taşıyan bir başlık (aynısı H1'de de kullanılıyor).
      title: 'Spor Takviyesi Kategorileri ve Fiyatları | ProteinAvcısı',
      description: 'Protein tozu, kreatin, amino asitler, pre-workout ve daha fazlası — takip ettiğimiz tüm spor takviyesi kategorilerini gerçek ürün sayılarıyla keşfet.',
      canonicalPath: '/kategoriler',
    });

    this.dealsService.getFilterOptions().subscribe({
      next: (options) => {
        if (options.categories.length === 0) {
          this.categories.set([]);
          this.loading.set(false);
          return;
        }

        // Her kategori için gerçek ürün sayısını çekiyoruz (pageSize:1,
        // sadece totalCount lazım) — uydurma/tahmini bir rakam göstermemek
        // için, ana sayfadaki "siteProductCount" ile aynı desen.
        const counts$ = options.categories.map((slug) =>
          this.dealsService.getAllProducts({ categories: [slug], pageSize: 1 }).pipe(
            map((result) => result.totalCount),
            catchError(() => of(0)),
          ),
        );

        forkJoin(counts$).subscribe((counts) => {
          const cards = options.categories
            .map((slug, i) => ({
              slug,
              label: CATEGORY_LABELS[slug] ?? slug,
              intro: CATEGORY_INTROS[slug] ?? '',
              productCount: counts[i],
              iconClass: categoryPhosphorIcon(slug),
              tone: CATEGORY_TONES[slug] ?? 'violet',
            }))
            .sort((a, b) => b.productCount - a.productCount);

          this.categories.set(cards);
          this.loading.set(false);
        });
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  protected setCategorySearch(value: string): void {
    this.categorySearch.set(value);
  }

  protected setFilter(filter: CategoryFilter): void {
    this.activeFilter.set(filter);
  }

  private normalizeSearch(value: string): string {
    return value
      .toLocaleLowerCase('tr-TR')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');
  }
}

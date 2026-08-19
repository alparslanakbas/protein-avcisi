import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { CATEGORY_INTROS, CATEGORY_LABELS } from '../core/category-labels';
import { DealsService } from '../core/deals.service';
import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

interface CategoryCard {
  slug: string;
  label: string;
  intro: string;
  productCount: number;
}

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
}

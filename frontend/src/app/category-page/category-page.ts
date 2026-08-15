import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CATEGORY_LABELS } from '../core/category-labels';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { PageMetaService } from '../core/page-meta.service';

type ViewMode = 'deals' | 'store' | 'all';
const PAGE_SIZE = 24;

// Bir geliştiricinin SEO geri bildirimi üzerine eklendi: kategori sayfaları
// marka sayfalarıyla aynı desende ama şablon metin yerine her kategori için
// gerçekten farklı, açıklayıcı bir giriş metni taşıyor — "ince içerik"
// (thin content) riskini azaltmak için bilinçli.
const CATEGORY_INTROS: Record<string, string> = {
  'protein-tozu':
    'Protein tozu, kas gelişimi ve günlük protein ihtiyacını karşılamak için en yaygın kullanılan spor takviyesi. Whey (peynir altı suyu) protein hızlı emilimiyle bilinirken, kazein daha yavaş salınım sağlar.',
  kreatin:
    'Kreatin, güç ve patlayıcı performans gerektiren antrenmanlarda en çok araştırılmış takviyelerden biri. Genellikle kreatin monohidrat formunda satılır ve düzenli günlük kullanımla etkisini gösterir.',
  'amino-asitler':
    'BCAA, EAA, glutamin ve arginin gibi amino asit takviyeleri, kas onarımı ve antrenman sonrası toparlanma sürecini desteklemek amacıyla tercih ediliyor.',
  'pre-workout':
    'Pre-workout ürünleri, antrenman öncesi enerji ve odaklanmayı artırmak amacıyla kafein, beta-alanin ve nitrik oksit destekleyici içerikler barındırır.',
  'yag-yakici':
    'Yağ yakıcı takviyeler, termojenik bileşenler içeren ve diyet ile antrenman programını desteklemek amacıyla kullanılan ürünler.',
  'kilo-hacim':
    'Kilo/hacim (gainer) ürünleri, yüksek kalori ve karbonhidrat içeriğiyle kilo almakta zorlanan veya hacim antrenmanı yapan kullanıcılar için tasarlandı.',
  vitamin:
    'Vitamin ve mineral takviyeleri, günlük beslenmedeki eksiklikleri tamamlamak amacıyla kullanılır — multivitaminden omega-3\'e, magnezyumdan çinkoya kadar geniş bir yelpazeyi kapsar.',
  'saglikli-atistirmaliklar':
    'Protein bar, kurabiye ve pirinç pastası gibi sağlıklı atıştırmalıklar, günlük protein/kalori hedefini pratik bir şekilde tamamlamak isteyenler için alternatif sunuyor.',
  'l-carnitine-cla':
    'L-Carnitine ve CLA (konjuge linoleik asit), yağ metabolizmasını desteklemek amacıyla kullanılan, genellikle diyet döneminde tercih edilen takviyeler.',
};

@Component({
  selector: 'app-category-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './category-page.html',
})
export class CategoryPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);

  protected readonly categorySlug = signal<string>('');
  protected readonly categoryLabel = signal<string>('');
  protected readonly categoryIntro = signal<string>('');
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

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('categorySlug') ?? '';
      this.loadCategory(slug);
    });
  }

  private loadCategory(slug: string): void {
    this.loading.set(true);
    this.viewMode.set('all');
    this.currentPage.set(1);

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
        this.otherCategories.set(
          options.categories
            .filter((c) => c !== match)
            .map((c) => ({ slug: c, label: CATEGORY_LABELS[c] ?? c })),
        );
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
    const query = { categories: [categorySlug], page: this.currentPage(), pageSize: PAGE_SIZE };
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

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadItems();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  private setMeta(label: string, slug: string): void {
    const title = `${label} Fiyatları ve İndirimleri | ProteinAvcısı`;
    const description = `${label} kategorisindeki güncel fiyatlar, gerçek fiyat geçmişine dayanan doğrulanmış indirimler ve mağaza kampanyaları. ProteinAvcısı, fiyatları düzenli olarak takip ediyor.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/kategori/${slug}`,
    });
  }

  protected discountBadge(deal: Deal): string {
    return `-%${deal.discountPercent}`;
  }

  protected storeDiscountBadge(deal: Deal): string {
    return `Mağaza -%${deal.storeDiscountPercent}`;
  }

  protected goToProduct(deal: Deal): void {
    this.router.navigate(['/urun', deal.productId]);
  }
}

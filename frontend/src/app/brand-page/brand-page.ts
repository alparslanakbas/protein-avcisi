import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Coupon } from '../core/coupon.model';
import { CouponsService } from '../core/coupons.service';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { PageMetaService } from '../core/page-meta.service';

type ViewMode = 'deals' | 'store' | 'all';
const PAGE_SIZE = 24;

@Component({
  selector: 'app-brand-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './brand-page.html',
})
export class BrandPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly couponsService = inject(CouponsService);
  private readonly pageMeta = inject(PageMetaService);

  protected readonly brandName = signal<string>('');
  protected readonly coupons = signal<Coupon[]>([]);
  protected readonly otherBrands = signal<string[]>([]);
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
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly currentPage = signal(1);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('brandSlug') ?? '';
      this.loadBrand(slug);
    });
  }

  private loadBrand(slug: string): void {
    this.loading.set(true);
    this.viewMode.set('all');
    this.currentPage.set(1);

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

        this.brandName.set(match);
        // Marka sayfaları birbirine link vermiyordu — diğer marka sayfalarına
        // iç linkleme için mevcut marka çıkarılmış listeyi ayrıca tutuyoruz.
        this.otherBrands.set(options.brands.filter((b) => b !== match));
        this.setMeta(match);

        this.couponsService.getCoupons().subscribe((coupons) => {
          this.coupons.set(coupons.filter((c) => c.brandName === match));
        });

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
    const query = { brands: [brand], page: this.currentPage(), pageSize: PAGE_SIZE };
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

  private setMeta(brand: string): void {
    const title = `${brand} İndirim Kodu ve Kampanyaları 2026 | ProteinAvcısı`;
    const description = `${brand} için güncel kupon kodları ve gerçek fiyat geçmişine dayanan doğrulanmış indirimler. ProteinAvcısı, ${brand} markasının fiyatlarını düzenli olarak takip ediyor.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/marka/${brand.toLowerCase()}/indirim-kodu`,
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

  // Alfabetik sıralama ile tek bir kanonik URL üretiyoruz (hiq-vs-ssn hep
  // aynı sırada) — aksi halde aynı içeriğe iki farklı URL'den erişilebilir
  // olurdu (duplicate content riski).
  protected comparisonPairSlug(otherBrand: string): string {
    const current = this.brandName().toLowerCase();
    const other = otherBrand.toLowerCase();
    return [current, other].sort().join('-vs-');
  }
}

import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ComparisonService } from '../core/comparison.service';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { slugify } from '../core/slugify';
import { SupplementDosage, findSupplementDosage } from '../core/supplement-dosages';
import { SiteHeader } from '../site-header/site-header';

interface DosageProduct {
  deal: Deal;
  totalGrams: number;
  daysSupply: number;
  costPerDay: number;
}

interface ExamplePair {
  a: Deal;
  b: Deal;
  slug: string;
}

// Ana sayfa/protein hesaplayıcısındaki aynı desen — tablo satır listesi
// olduğu için 12 makul bir sayfa boyutu.
const PAGE_SIZE = 12;
const SEARCH_DEBOUNCE_MS = 350;

// Kreatin/beta-alanine/sitrülin/betain/EAA için TEK bileşen, konfigürasyonla
// (supplement-dosages.ts) beş ayrı sayfa üretiyor. Her birine ayrı bileşen
// yazmak yüzlerce satır kod tekrarı olurdu; BrandPage'in iki modlu
// çalışmasıyla aynı yaklaşım.
//
// ÖNEMLİ TASARIM KARARI: bu takviyelerin dozu KİLOYA GÖRE ÖLÇEKLENMEZ —
// literatürde ve pratikte sabit aralıklar kullanılır. "Kilonu gir, dozunu
// hesaplayalım" tarzı bir araç uydurma olurdu. Bunun yerine dürüst bir doz
// aralığı gösterilip, asıl hesap bizim gerçek veri avantajımız üzerinden
// yapılıyor: seçilen paket kaç gün yeter, günlük maliyeti ne kadar.
@Component({
  selector: 'app-supplement-dosage-page',
  imports: [DecimalPipe, FormsModule, RouterLink, SiteHeader],
  templateUrl: './supplement-dosage-page.html',
})
export class SupplementDosagePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly config = signal<SupplementDosage | null>(null);
  protected readonly dailyGrams = signal<number>(0);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly products = signal<Deal[]>([]);

  // Protein hesaplayıcısındaki aynı desen — kategori zaten tek bir istekte
  // (≤100 ürün) tamamen çekildiği için arama/marka filtresi CLIENT-SIDE
  // yapılıyor, ekstra bir ağ isteği gerekmiyor. Sayfalama da öyle (dailyGrams
  // değişince maliyet/sıralama anında yeniden hesaplanıyor, sunucuya gitmeden).
  protected readonly searchQuery = signal('');
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  protected readonly currentPage = signal(1);
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  protected readonly availableBrands = computed(() =>
    [...new Set(this.products().map((p) => p.brandName))].sort(),
  );

  protected readonly hasActiveFilters = computed(
    () => this.searchQuery().trim().length > 0 || this.selectedBrands().size > 0,
  );

  // Örnek karşılaştırma çiftleri — aynı kategoriden, sayfa yüklenince BİR
  // KEZ rastgele seçiliyor (loadProducts'ta), sonraki her render'da aynı
  // kalıyor. "Alakalı" şartı zaten sağlanıyor çünkü products() bu sayfanın
  // kategorisine scope'lu.
  protected readonly examplePairs = signal<ExamplePair[]>([]);

  // Seçilen günlük doza göre: her ürün kaç gün yeter, günlük maliyeti ne.
  // Yalnızca paket gramajı BİLİNEN ürünler listeleniyor — bilinmeyen için
  // tahmin yürütmüyoruz. Arama/marka filtresi burada, sıralamadan ÖNCE
  // uygulanıyor.
  protected readonly dosageProducts = computed<DosageProduct[]>(() => {
    const grams = this.dailyGrams();
    if (!grams || grams <= 0) return [];

    const query = this.searchQuery().trim().toLowerCase();
    const brands = this.selectedBrands();

    return this.products()
      .filter((deal) => !query || deal.productName.toLowerCase().includes(query))
      .filter((deal) => brands.size === 0 || brands.has(deal.brandName))
      .map((deal) => {
        const totalGrams = this.packageGrams(deal);
        if (!totalGrams) return null;

        const daysSupply = totalGrams / grams;
        if (daysSupply < 1) return null;

        return { deal, totalGrams, daysSupply, costPerDay: deal.currentPrice / daysSupply };
      })
      .filter((x): x is DosageProduct => x !== null)
      .sort((a, b) => a.costPerDay - b.costPerDay);
  });

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.dosageProducts().length / PAGE_SIZE)));

  protected readonly pagedDosageProducts = computed(() => {
    const all = this.dosageProducts();
    const page = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * PAGE_SIZE;
    return all.slice(start, start + PAGE_SIZE);
  });

  protected onDailyGramsChange(value: number): void {
    this.dailyGrams.set(value);
    this.currentPage.set(1);
  }

  protected onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => this.currentPage.set(1), SEARCH_DEBOUNCE_MS);
  }

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    current.has(brand) ? current.delete(brand) : current.add(brand);
    this.selectedBrands.set(current);
    this.currentPage.set(1);
  }

  protected clearFilters(): void {
    this.searchQuery.set('');
    this.selectedBrands.set(new Set());
    this.currentPage.set(1);
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
  }

  protected productLink(deal: Deal): string[] {
    return ['/urun', String(deal.productId), slugify(deal.productName)];
  }

  // Paketteki toplam gram. İki kaynak var:
  // (1) Size alanı ("300 Gr" / "2 Kg") — üç markada bu geliyor;
  // (2) paket servis sayısı × servis gramajı — ProteinOcean'da Size hiç
  //     gelmiyor ama ikisi de markanın kendi verisinden geldiği için bu
  //     çarpım türetilmiş bir tahmin değil.
  // Bu ikinci yol olmadan ProteinOcean ürünleri tabloya hiç giremiyordu —
  // protein tozu tablosunda çözülen aynı sorun (bkz. CalculateServings).
  private packageGrams(deal: Deal): number | null {
    const fromSize = this.parsePackageGrams(deal.size);
    if (fromSize) return fromSize;

    if (deal.servingsPerPackage && deal.servingsPerPackage > 0 && deal.servingSizeGrams && deal.servingSizeGrams > 0) {
      return deal.servingsPerPackage * deal.servingSizeGrams;
    }

    return null;
  }

  private parsePackageGrams(size: string | null): number | null {
    if (!size) return null;
    const match = /^(\d+(?:[.,]\d+)?)\s*(Gr|Kg)$/i.exec(size.trim());
    if (!match) return null;

    const value = Number(match[1].replace(',', '.'));
    if (!value) return null;

    return match[2].toLowerCase() === 'kg' ? value * 1000 : value;
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug') ?? '';
      const config = findSupplementDosage(slug);

      if (!config) {
        // Soft-404 yerine gerçek yönlendirme — projedeki yerleşik desen.
        this.router.navigate(['/hesaplama']);
        return;
      }

      this.config.set(config);
      this.dailyGrams.set(config.defaultDailyGrams);
      // Bileşen aynı örnek üzerinden bir doz sayfasından diğerine (ör.
      // kreatin-dozu → beta-alanine-dozu) yeniden kullanılıyor — eski
      // sayfanın filtre/sayfa durumu yeni sayfaya sızmasın.
      this.searchQuery.set('');
      this.selectedBrands.set(new Set());
      this.currentPage.set(1);
      this.setMeta(config);
      this.loadProducts(config);
    });
  }

  private setMeta(config: SupplementDosage): void {
    this.pageMeta.set({
      title: config.title,
      description: config.description,
      canonicalPath: `/hesaplama/${config.slug}`,
    });

    this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, {
      '@context': 'https://schema.org',
      '@type': 'WebApplication',
      name: config.h1,
      applicationCategory: 'HealthApplication',
      operatingSystem: 'Web',
      offers: { '@type': 'Offer', price: '0', priceCurrency: 'TRY' },
    });
  }

  private loadProducts(config: SupplementDosage): void {
    this.loading.set(true);
    this.loadError.set(false);

    this.dealsService
      // expandSynonyms: false — "alanine" araması, o kelime amino-asitler
      // kategorisinin anahtar kelimelerinden biri olduğu için kategorinin
      // TAMAMINI getiriyordu (arginin ürünleri beta-alanine sayfasında
      // listeleniyordu). Burada tam da o tek bileşeni arıyoruz.
      .getAllProducts({
        categories: config.category ? [config.category] : [],
        search: config.searchTerm ?? undefined,
        pageSize: 100,
        expandSynonyms: false,
      })
      .subscribe({
        next: (result) => {
          this.products.set(result.items);
          this.examplePairs.set(this.pickExamplePairs(result.items));
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
  }

  // 3 rastgele, birbirinden farklı ürün çifti — hepsi aynı kategoriden
  // olduğu için zaten "alakalı". Fisher-Yates'e gerek yok, sadece birkaç
  // çift seçtiğimiz için basit bir rastgele karıştırma yeterli.
  private pickExamplePairs(list: Deal[]): ExamplePair[] {
    if (list.length < 2) return [];

    const shuffled = [...list].sort(() => Math.random() - 0.5);
    const pairs: ExamplePair[] = [];
    for (let i = 0; i + 1 < shuffled.length && pairs.length < 3; i += 2) {
      const [a, b] = [shuffled[i], shuffled[i + 1]];
      pairs.push({ a, b, slug: ComparisonService.pairSlug(a.productId, b.productId) });
    }
    return pairs;
  }
}

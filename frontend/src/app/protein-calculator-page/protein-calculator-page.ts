import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { DOCUMENT } from '@angular/common';
import { slugify } from '../core/slugify';
import { SiteHeader } from '../site-header/site-header';

interface ActivityLevel {
  id: string;
  label: string;
  description: string;
  // Kilo başına gram protein aralığı — sektörde ve beslenme kaynaklarında
  // yaygın kabul gören değerler, uydurma değil. Tek bir sayı yerine ARALIK
  // veriyoruz çünkü gerçekte de tek bir doğru sayı yok.
  minPerKg: number;
  maxPerKg: number;
}

const ACTIVITY_LEVELS: ActivityLevel[] = [
  {
    id: 'sedentary',
    label: 'Hareketsiz',
    description: 'Düzenli spor yapmıyorum',
    minPerKg: 0.8,
    maxPerKg: 1.0,
  },
  {
    id: 'active',
    label: 'Düzenli egzersiz',
    description: 'Haftada 3-5 gün antrenman',
    minPerKg: 1.2,
    maxPerKg: 1.6,
  },
  {
    id: 'intense',
    label: 'Yoğun antrenman',
    description: 'Kas kazanımı odaklı, haftada 5+ gün',
    minPerKg: 1.6,
    maxPerKg: 2.2,
  },
];

interface ProductValue {
  deal: Deal;
  pricePerServing: number;
  proteinServings: number;
}

// Tablo, ürün grid'lerinden (24) daha küçük bir sayfa boyutu kullanıyor —
// hem satır listesi olduğu için hem de SSR çıktısını hafif tutmak adına
// (sayfanın ilk hali tüm kategoriyi gömüp 451 KB'a çıkmıştı).
const PAGE_SIZE = 12;
const SEARCH_DEBOUNCE_MS = 350;

// Bu sayfanın fikri: "günlük protein ihtiyacı hesaplama" araması yüksek
// hacimli ve rakiplerin çoğunda bir hesaplayıcı var — ama hiçbirinde CANLI
// fiyat verisi yok. Bizde ikisi de olduğu için hesaplama sonucunu doğrudan
// "servis başı en uygun ürün" listesine bağlayabiliyoruz. Öneri listesi
// yalnızca porsiyon büyüklüğü GERÇEKTEN bilinen ürünlerden kuruluyor —
// "30 gr = 1 servis" gibi bir varsayım bu projede hiç yapılmadı.
@Component({
  selector: 'app-protein-calculator-page',
  imports: [DecimalPipe, FormsModule, RouterLink, SiteHeader],
  templateUrl: './protein-calculator-page.html',
})
export class ProteinCalculatorPage implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly activityLevels = ACTIVITY_LEVELS;

  protected readonly weight = signal<number | null>(null);
  protected readonly activityId = signal<string>('active');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  private readonly products = signal<Deal[]>([]);

  // Tablo filtreleri — ana sayfadaki aynı desen (arama debounce'lu,
  // marka çipleri, sayfalama). İlk hali yalnızca 6 ürün gösteriyordu ve
  // o 6'sının hepsi tek markadan çıkıyordu; kullanıcı bunu fark edip
  // "protein tozlarının tamamını, arama kutusuyla birlikte" istedi.
  protected readonly searchQuery = signal('');
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  protected readonly availableBrands = signal<string[]>([]);
  protected readonly currentPage = signal(1);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  protected readonly hasActiveFilters = computed(
    () => this.searchQuery().trim().length > 0 || this.selectedBrands().size > 0,
  );

  protected readonly selectedActivity = computed(
    () => ACTIVITY_LEVELS.find((level) => level.id === this.activityId()) ?? ACTIVITY_LEVELS[1],
  );

  // Kilo girilmeden hiçbir sonuç göstermiyoruz — varsayılan bir kiloyla
  // (ör. 70 kg) doldurup "senin ihtiyacın şu" demek yanıltıcı olurdu.
  protected readonly dailyProtein = computed(() => {
    const kg = this.weight();
    if (!kg || kg <= 0 || kg > 400) return null;

    const level = this.selectedActivity();
    return {
      min: Math.round(kg * level.minPerKg),
      max: Math.round(kg * level.maxPerKg),
    };
  });

  // Backend zaten servis başı fiyata göre sıralı ve elenmiş bir liste
  // döndürüyor (bkz. GetBestValuePerServingAsync); burada sadece gösterim
  // için servis sayısı/birim fiyat tekrar hesaplanıyor.
  protected readonly bestValueProducts = computed<ProductValue[]>(() =>
    this.products()
      .map((deal) => {
        const servings = this.calculateServings(deal);
        if (!servings || servings < 1) return null;

        return {
          deal,
          pricePerServing: deal.currentPrice / servings,
          proteinServings: servings,
        };
      })
      .filter((item): item is ProductValue => item !== null),
  );

  protected productLink(deal: Deal): string[] {
    return ['/urun', String(deal.productId), slugify(deal.productName)];
  }

  // Backend'deki DealsQueryService.CalculateServings ile aynı öncelik:
  // markanın doğrudan beyan ettiği servis sayısı (ProteinOcean) varsa o,
  // yoksa paket gramajı ÷ porsiyon büyüklüğü.
  private calculateServings(deal: Deal): number | null {
    if (deal.servingsPerPackage && deal.servingsPerPackage > 0) return deal.servingsPerPackage;

    const packageGrams = this.parsePackageGrams(deal.size);
    if (packageGrams && deal.servingSizeGrams && deal.servingSizeGrams > 0) {
      return packageGrams / deal.servingSizeGrams;
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

  protected onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.currentPage.set(1);
      this.loadProducts();
    }, SEARCH_DEBOUNCE_MS);
  }

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    current.has(brand) ? current.delete(brand) : current.add(brand);
    this.selectedBrands.set(current);
    this.currentPage.set(1);
    this.loadProducts();
  }

  protected clearFilters(): void {
    this.searchQuery.set('');
    this.selectedBrands.set(new Set());
    this.currentPage.set(1);
    this.loadProducts();
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadProducts();
  }

  private loadProducts(): void {
    this.loading.set(true);
    this.loadError.set(false);

    this.dealsService
      .getBestValuePerServing({
        category: 'protein-tozu',
        brands: [...this.selectedBrands()],
        search: this.searchQuery().trim() || undefined,
        page: this.currentPage(),
        pageSize: PAGE_SIZE,
      })
      .subscribe({
        next: (result) => {
          this.products.set(result.items);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
  }

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Günlük Protein İhtiyacı Hesaplama | ProteinAvcısı',
      description:
        'Kilona ve antrenman yoğunluğuna göre günlük protein ihtiyacını hesapla, sonucu doğrudan güncel fiyatlarla karşılaştır — servis başı en uygun protein tozu ürünlerini gör.',
      canonicalPath: '/hesaplama/protein-ihtiyaci',
    });

    // Google'ın hesaplayıcı sayfalarını anlamasına yardımcı olan yapısal veri.
    this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, {
      '@context': 'https://schema.org',
      '@type': 'WebApplication',
      name: 'Günlük Protein İhtiyacı Hesaplama',
      applicationCategory: 'HealthApplication',
      operatingSystem: 'Web',
      offers: { '@type': 'Offer', price: '0', priceCurrency: 'TRY' },
    });

    // Marka çipleri: yalnızca bu kategoride servis başı fiyatı gerçekten
    // hesaplanabilen markalar (boş sonuç veren çip göstermemek için).
    this.dealsService.getBestValueBrands('protein-tozu').subscribe({
      next: (brands) => this.availableBrands.set(brands),
      error: () => this.availableBrands.set([]),
    });

    this.loadProducts();
  }
}

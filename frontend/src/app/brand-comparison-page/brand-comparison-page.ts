import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { BrandComparison } from '../core/brand-comparison.model';
import { BrandComparisonService } from '../core/brand-comparison.service';
import { CATEGORY_LABELS } from '../core/category-labels';
import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-brand-comparison-page',
  imports: [DecimalPipe, RouterLink, SiteHeader],
  templateUrl: './brand-comparison-page.html',
})
export class BrandComparisonPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly comparisonService = inject(BrandComparisonService);
  private readonly pageMeta = inject(PageMetaService);

  protected readonly comparison = signal<BrandComparison | null>(null);
  protected readonly loading = signal(true);
  protected readonly pairSlug = signal('');

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const pair = params.get('pair') ?? '';
      this.loadComparison(pair);
    });
  }

  private loadComparison(pair: string): void {
    this.loading.set(true);
    const parts = pair.split('-vs-');
    if (parts.length !== 2 || !parts[0] || !parts[1]) {
      this.router.navigate(['/']);
      return;
    }

    this.pairSlug.set(pair);
    this.comparisonService.compare(parts[0], parts[1]).subscribe({
      next: (result) => {
        this.comparison.set(result);
        this.setMeta(result);
        this.loading.set(false);
      },
      error: () => this.router.navigate(['/']),
    });
  }

  private setMeta(comparison: BrandComparison): void {
    const title = `${comparison.brand1} vs ${comparison.brand2} Fiyat Karşılaştırması | ProteinAvcısı`;
    const description = `${comparison.brand1} ve ${comparison.brand2} markalarının kategori bazında güncel ortalama fiyatlarını karşılaştır — gerçek fiyat verisine dayanır.`;

    this.pageMeta.set({
      title,
      description,
      canonicalPath: `/karsilastir/${this.pairSlug()}`,
    });
  }

  protected categoryLabel(category: string): string {
    return CATEGORY_LABELS[category] ?? category;
  }

  protected cheaperBrand(cat: { brand1AvgPrice: number | null; brand2AvgPrice: number | null }): 1 | 2 | null {
    if (cat.brand1AvgPrice === null || cat.brand2AvgPrice === null) return null;
    if (cat.brand1AvgPrice === cat.brand2AvgPrice) return null;
    return cat.brand1AvgPrice < cat.brand2AvgPrice ? 1 : 2;
  }

  // Tablonun altına kısa bir özet paragrafı — kaç kategoride hangi markanın
  // daha ucuz olduğu + en belirgin farkın hangi kategoride olduğu. Tamamen
  // mevcut kategori verisinden türetiliyor, ekstra bir backend çağrısı
  // gerekmiyor (dış bir kod incelemesinde önerildi: "sadece tablo, hiç
  // yorum yok" eleştirisine cevap).
  //
  // "leader" alanı BİLİNÇLİ OLARAK eklendi (2026-08-24, ikinci bir dış
  // inceleme bulgusu) — önceki şablon "daha ucuz olan taraf: {brand1}
  // (Nwins), {brand2} (Mwins)" şeklinde brand1'i (alfabetik ilk marka,
  // kazanan olsun olmasın) HER ZAMAN önce yazıyordu; "Hardline (0
  // kategori), HIQ (7 kategori)" gibi Hardline'ı "daha ucuz taraf" diye
  // açıp sonra 0 diyen kafa karıştırıcı cümleler üretiyordu. Artık kazanan
  // marka ayrıca hesaplanıp şablonda TEK ve NET bir özne olarak kullanılıyor.
  protected readonly summary = computed(() => {
    const c = this.comparison();
    if (!c || c.categories.length === 0) return null;

    let brand1Wins = 0;
    let brand2Wins = 0;
    let biggestDiff: { category: string; percent: number; cheaper: 1 | 2 } | null = null;

    for (const cat of c.categories) {
      const winner = this.cheaperBrand(cat);
      if (winner === 1) brand1Wins++;
      else if (winner === 2) brand2Wins++;

      if (winner !== null && cat.brand1AvgPrice !== null && cat.brand2AvgPrice !== null) {
        const higher = winner === 1 ? cat.brand2AvgPrice : cat.brand1AvgPrice;
        const lower = winner === 1 ? cat.brand1AvgPrice : cat.brand2AvgPrice;
        const percent = Math.round(((higher - lower) / higher) * 100);
        if (!biggestDiff || percent > biggestDiff.percent) {
          biggestDiff = { category: cat.category, percent, cheaper: winner };
        }
      }
    }

    const ties = c.categories.length - brand1Wins - brand2Wins;
    const leader: 1 | 2 | null = brand1Wins === brand2Wins ? null : brand1Wins > brand2Wins ? 1 : 2;
    const leaderWins = leader === 1 ? brand1Wins : leader === 2 ? brand2Wins : 0;
    const otherWins = leader === 1 ? brand2Wins : leader === 2 ? brand1Wins : 0;

    return { brand1Wins, brand2Wins, ties, leader, leaderWins, otherWins, biggestDiff };
  });
}

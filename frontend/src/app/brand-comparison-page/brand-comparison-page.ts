import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { BrandComparison } from '../core/brand-comparison.model';
import { BrandComparisonService } from '../core/brand-comparison.service';
import { CATEGORY_LABELS } from '../core/category-labels';
import { PageMetaService } from '../core/page-meta.service';

@Component({
  selector: 'app-brand-comparison-page',
  imports: [DecimalPipe, RouterLink],
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
}

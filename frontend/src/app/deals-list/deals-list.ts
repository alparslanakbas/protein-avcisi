import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';

import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';

type ViewMode = 'deals' | 'all';

@Component({
  selector: 'app-deals-list',
  imports: [DecimalPipe],
  templateUrl: './deals-list.html',
  styleUrl: './deals-list.scss',
})
export class DealsList implements OnInit {
  private readonly dealsService = inject(DealsService);

  protected readonly deals = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly viewMode = signal<ViewMode>('deals');

  ngOnInit(): void {
    this.load();
  }

  protected setViewMode(mode: ViewMode): void {
    if (this.viewMode() === mode) return;
    this.viewMode.set(mode);
    this.load();
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
}

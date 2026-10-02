import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { LatestRequest } from '../core/latest-request';
import { PriceHistoryService } from '../core/price-history.service';
import { productPath } from '../core/product-link';
import { GoalPickSection } from '../core/supplement-goals';
import { ValuePick } from '../core/value-pick.model';

// Liste kısa tutuluyor: sayfanın işi karar vermek, katalog gezdirmek değil.
// Tamamı için her bölüm kategori sayfasına bağlanıyor.
const PICK_COUNT = 6;

const PROTEIN_TYPES: { value: string | null; label: string }[] = [
  { value: null, label: 'Tümü' },
  { value: 'izole', label: 'Laktozu düşük (izole)' },
  { value: 'bitkisel', label: 'Bitkisel' },
];

// "Hangi takviye?" hedef sayfasındaki bir ürün listesi: kategoride kilogram
// fiyatına göre, her markadan bir ürün (bkz. /api/value-picks).
@Component({
  selector: 'app-value-pick-list',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './value-pick-list.html',
})
export class ValuePickList implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly request = new LatestRequest();

  readonly section = input.required<GoalPickSection>();
  /** Testin süt tercihinden gelen başlangıç türü (yalnızca protein listesinde). */
  readonly initialType = input<string | null>(null);

  protected readonly displayName = displayName;
  protected readonly productPath = productPath;
  protected readonly proteinTypes = PROTEIN_TYPES;

  protected readonly type = signal<string | null>(null);
  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly picks = signal<ValuePick[]>([]);
  protected readonly eligibleCount = signal(0);
  protected readonly skeletonRows = Array.from({ length: 4 }, (_, i) => i);

  ngOnInit(): void {
    const section = this.section();
    this.type.set(section.proteinFilter ? this.initialType() : (section.type ?? null));
    this.load();
  }

  protected setProteinType(value: string | null): void {
    if (this.type() === value) return;
    this.type.set(value);
    this.load();
  }

  protected trackStoreClick(pick: ValuePick): void {
    this.priceHistoryService.trackStoreClick(pick.productId);
  }

  private load(): void {
    this.state.set('loading');
    this.request.run(this.dealsService.getValuePicks(this.section().category, this.type(), PICK_COUNT), {
      next: (result) => {
        this.picks.set(result.items);
        this.eligibleCount.set(result.eligibleCount);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}

import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';

import { Deal } from '../core/deal.model';
import { PriceHistoryService } from '../core/price-history.service';

interface TimeRangeOption {
  label: string;
  days: number;
}

const TIME_RANGES: TimeRangeOption[] = [
  { label: '7G', days: 7 },
  { label: '15G', days: 15 },
  { label: '1A', days: 30 },
  { label: '6A', days: 180 },
  { label: '1Y', days: 365 },
];

const CHART_WIDTH = 600;
const CHART_HEIGHT = 220;
const CHART_PADDING_Y = 16;

@Component({
  selector: 'app-product-modal',
  imports: [DecimalPipe],
  templateUrl: './product-modal.html',
})
export class ProductModal {
  private readonly priceHistoryService = inject(PriceHistoryService);

  readonly deal = input.required<Deal>();
  readonly closed = output<void>();

  protected readonly timeRanges = TIME_RANGES;
  protected readonly selectedRange = signal<TimeRangeOption>(TIME_RANGES[2]);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly points = signal<{ price: number; scrapedAt: string }[]>([]);
  protected readonly minPrice = signal(0);
  protected readonly maxPrice = signal(0);
  protected readonly currentPrice = signal(0);

  protected readonly chartAreaPath = computed(() => this.buildAreaPath(this.points(), this.minPrice(), this.maxPrice()));
  protected readonly chartLinePath = computed(() => this.buildLinePath(this.points(), this.minPrice(), this.maxPrice()));

  protected readonly savingsText = computed(() => {
    const max = this.maxPrice();
    const current = this.currentPrice();
    if (max <= 0 || current >= max) return null;
    const diff = max - current;
    const percent = Math.round((diff / max) * 100);
    return { diff, percent };
  });

  constructor() {
    effect(() => {
      // deal() veya selectedRange() değişince yeniden çek.
      const deal = this.deal();
      const range = this.selectedRange();
      this.load(deal.productId, range.days);
    });
  }

  protected selectRange(range: TimeRangeOption): void {
    this.selectedRange.set(range);
  }

  protected close(): void {
    this.closed.emit();
  }

  protected goToStoreUrl(): string {
    return this.priceHistoryService.goToStoreUrl(this.deal().productId);
  }

  private load(productId: number, days: number): void {
    this.loading.set(true);
    this.error.set(null);

    this.priceHistoryService.get(productId, days).subscribe({
      next: (history) => {
        this.points.set(history.points);
        this.minPrice.set(history.minPrice);
        this.maxPrice.set(history.maxPrice);
        this.currentPrice.set(history.currentPrice);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Fiyat geçmişi yüklenemedi.');
        this.loading.set(false);
      },
    });
  }

  private buildLinePath(points: { price: number; scrapedAt: string }[], min: number, max: number): string {
    const coords = this.toCoordinates(points, min, max);
    if (coords.length === 0) return '';
    if (coords.length === 1) {
      const [[x, y]] = coords;
      return `M ${x - 4} ${y} L ${x + 4} ${y}`;
    }
    return coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ');
  }

  private buildAreaPath(points: { price: number; scrapedAt: string }[], min: number, max: number): string {
    const coords = this.toCoordinates(points, min, max);
    if (coords.length === 0) return '';
    const first = coords[0];
    const last = coords[coords.length - 1];
    const line = coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ');
    return `${line} L ${last[0]} ${CHART_HEIGHT} L ${first[0]} ${CHART_HEIGHT} Z`;
  }

  private toCoordinates(points: { price: number; scrapedAt: string }[], min: number, max: number): [number, number][] {
    if (points.length === 0) return [];

    const times = points.map((p) => new Date(p.scrapedAt).getTime());
    const minTime = Math.min(...times);
    const maxTime = Math.max(...times);
    const timeSpan = maxTime - minTime || 1;
    const priceSpan = max - min || 1;

    return points.map((p, i) => {
      const t = new Date(p.scrapedAt).getTime();
      const x = points.length === 1 ? CHART_WIDTH / 2 : ((t - minTime) / timeSpan) * CHART_WIDTH;
      const y =
        CHART_HEIGHT -
        CHART_PADDING_Y -
        ((p.price - min) / priceSpan) * (CHART_HEIGHT - CHART_PADDING_Y * 2);
      return [x, y] as [number, number];
    });
  }
}

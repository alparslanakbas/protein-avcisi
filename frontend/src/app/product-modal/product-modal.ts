import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';

import { Deal } from '../core/deal.model';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';

interface TimeRangeOption {
  label: string;
  days: number;
  periodName: string;
}

const TIME_RANGES: TimeRangeOption[] = [
  { label: '7G', days: 7, periodName: 'son 7 günün' },
  { label: '15G', days: 15, periodName: 'son 15 günün' },
  { label: '1A', days: 30, periodName: 'son 1 ayın' },
  { label: '6A', days: 180, periodName: 'son 6 ayın' },
  { label: '1Y', days: 365, periodName: 'son 1 yılın' },
];

const CHART_WIDTH = 600;
const CHART_HEIGHT = 220;
const CHART_PADDING_Y = 16;
const AXIS_LABEL_COUNT = 5;

const axisDateFormatter = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short' });
const tooltipDateFormatter = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' });

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
  protected readonly lastCheckedText = computed(() => formatRelativeTime(this.deal().scrapedAt));

  // "loading": bir istek sürüyor. "hasData": en az bir kez veri geldi.
  // Sekme değişiminde eski grafiği gizlemek yerine üstünde soluk bir
  // yükleniyor efekti gösteriyoruz — tüm modal her tıklamada "refresh"
  // atmış gibi görünmesin diye.
  protected readonly loading = signal(true);
  protected readonly hasData = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly points = signal<{ price: number; scrapedAt: string }[]>([]);
  protected readonly minPrice = signal(0);
  protected readonly maxPrice = signal(0);
  protected readonly currentPrice = signal(0);
  protected readonly hoverIndex = signal<number | null>(null);

  protected readonly coordinates = computed(() => this.toCoordinates(this.points(), this.minPrice(), this.maxPrice()));
  protected readonly chartAreaPath = computed(() => this.buildAreaPath(this.coordinates()));
  protected readonly chartLinePath = computed(() => this.buildLinePath(this.coordinates()));
  protected readonly xAxisLabels = computed(() => this.buildXAxisLabels(this.points()));

  protected readonly hoverInfo = computed(() => {
    const idx = this.hoverIndex();
    if (idx === null) return null;
    const coords = this.coordinates();
    const pts = this.points();
    if (idx >= coords.length || idx >= pts.length) return null;

    const [x, y] = coords[idx];
    // Tooltip nokta ile birlikte kayıyor; grafiğin kenarlarına çok yakınken
    // ortalı hizalama tooltip'in yarısını taşırıp kırpılmasına yol açıyordu
    // (modal overflow-y-auto olduğu için overflow-x da örtük kısıtlanıyor).
    // Kenara yakınsa hizalamayı o yöne kaydırıyoruz.
    const align: 'left' | 'center' | 'right' = x < CHART_WIDTH * 0.12 ? 'left' : x > CHART_WIDTH * 0.88 ? 'right' : 'center';

    return {
      x,
      y,
      align,
      price: pts[idx].price,
      dateLabel: tooltipDateFormatter.format(new Date(pts[idx].scrapedAt)),
    };
  });

  protected readonly savingsText = computed(() => {
    const max = this.maxPrice();
    const current = this.currentPrice();
    if (max <= 0 || current >= max) return null;
    const diff = max - current;
    const percent = Math.round((diff / max) * 100);
    return { diff, percent, periodName: this.selectedRange().periodName };
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

  protected onChartMouseMove(event: MouseEvent): void {
    const coords = this.coordinates();
    if (coords.length === 0) return;

    const svg = event.currentTarget as SVGSVGElement;
    const rect = svg.getBoundingClientRect();
    const scaleX = CHART_WIDTH / rect.width;
    const svgX = (event.clientX - rect.left) * scaleX;

    let nearestIndex = 0;
    let nearestDist = Infinity;
    coords.forEach(([x], i) => {
      const dist = Math.abs(x - svgX);
      if (dist < nearestDist) {
        nearestDist = dist;
        nearestIndex = i;
      }
    });
    this.hoverIndex.set(nearestIndex);
  }

  protected onChartMouseLeave(): void {
    this.hoverIndex.set(null);
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
        this.hasData.set(true);
      },
      error: () => {
        this.error.set('Fiyat geçmişi yüklenemedi.');
        this.loading.set(false);
      },
    });
  }

  private buildLinePath(coords: [number, number][]): string {
    if (coords.length === 0) return '';
    if (coords.length === 1) {
      const [[x, y]] = coords;
      return `M ${x - 4} ${y} L ${x + 4} ${y}`;
    }
    return coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ');
  }

  private buildAreaPath(coords: [number, number][]): string {
    if (coords.length === 0) return '';
    const first = coords[0];
    const last = coords[coords.length - 1];
    const line = coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ');
    return `${line} L ${last[0]} ${CHART_HEIGHT} L ${first[0]} ${CHART_HEIGHT} Z`;
  }

  private buildXAxisLabels(points: { price: number; scrapedAt: string }[]): { x: number; label: string }[] {
    if (points.length === 0) return [];

    const times = points.map((p) => new Date(p.scrapedAt).getTime());
    const minTime = Math.min(...times);
    const maxTime = Math.max(...times);

    if (maxTime === minTime) {
      return [{ x: CHART_WIDTH / 2, label: axisDateFormatter.format(new Date(minTime)) }];
    }

    return Array.from({ length: AXIS_LABEL_COUNT }, (_, i) => {
      const fraction = i / (AXIS_LABEL_COUNT - 1);
      const t = minTime + (maxTime - minTime) * fraction;
      return { x: fraction * CHART_WIDTH, label: axisDateFormatter.format(new Date(t)) };
    });
  }

  private toCoordinates(points: { price: number; scrapedAt: string }[], min: number, max: number): [number, number][] {
    if (points.length === 0) return [];

    const times = points.map((p) => new Date(p.scrapedAt).getTime());
    const minTime = Math.min(...times);
    const maxTime = Math.max(...times);
    const timeSpan = maxTime - minTime || 1;

    // Fiyat hiç değişmemişse (min===max) çizgi grafiğin dibine yapışıp
    // "boş" görünüyordu — bu durumda görsel aralığı fiyatın etrafında
    // yapay olarak genişletip çizgiyi dikeyde ortalıyoruz.
    let effectiveMin = min;
    let effectiveMax = max;
    if (max - min < 0.01) {
      const padding = Math.max(max * 0.05, 1);
      effectiveMin = min - padding;
      effectiveMax = max + padding;
    }
    const priceSpan = effectiveMax - effectiveMin;

    return points.map((p, i) => {
      const t = new Date(p.scrapedAt).getTime();
      const x = points.length === 1 ? CHART_WIDTH / 2 : ((t - minTime) / timeSpan) * CHART_WIDTH;
      const y =
        CHART_HEIGHT -
        CHART_PADDING_Y -
        ((p.price - effectiveMin) / priceSpan) * (CHART_HEIGHT - CHART_PADDING_Y * 2);
      return [x, y] as [number, number];
    });
  }
}

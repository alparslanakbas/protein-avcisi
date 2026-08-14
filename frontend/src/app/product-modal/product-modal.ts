import { DOCUMENT, DecimalPipe, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { canonicalOrigin } from '../core/canonical-link';
import { Deal } from '../core/deal.model';
import { PriceHistoryService } from '../core/price-history.service';
import { ProductFeedbackService } from '../core/product-feedback.service';
import { formatRelativeTime } from '../core/relative-time';
import { WatchService } from '../core/watch.service';
import { ShareButton } from '../share-button/share-button';

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
// Aynı gün içinde fiyat gerçekten değiştiyse (dedupeSameDaySamePrice sonrası
// hâlâ aynı güne ait birden fazla nokta kaldıysa) sadece tarih yetersiz
// kalıyor — o durumda saat de eklenip hangi anda değiştiği gösteriliyor.
const tooltipDateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

@Component({
  selector: 'app-product-modal',
  imports: [DecimalPipe, ShareButton, RouterLink, FormsModule],
  templateUrl: './product-modal.html',
})
export class ProductModal {
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly watchService = inject(WatchService);
  private readonly productFeedbackService = inject(ProductFeedbackService);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly deal = input.required<Deal>();
  readonly closed = output<void>();

  protected readonly shareUrl = computed(() => `${canonicalOrigin(this.document)}/urun/${this.deal().productId}`);

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

  // "Haber Ver" — bu ürünün fiyatı bir sonraki taramada düşerse tek
  // seferlik bir bildirim e-postası almak için.
  protected readonly watchFormOpen = signal(false);
  protected readonly watchEmail = signal('');
  protected readonly watchSubmitting = signal(false);
  protected readonly watchStatusMessage = signal<string | null>(null);

  // "Bu bilgi faydalı mıydı?" — basit güven sinyali, tekrar oy vermeyi
  // (auth olmadığı için) localStorage ile engelliyoruz, backend'de dedup yok.
  protected readonly votedHelpful = signal<boolean | null>(null);

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
      dateLabel: this.tooltipDateLabel(pts, idx),
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

    // Modal aynı bileşen örneği üzerinden farklı ürünler arasında yeniden
    // kullanıldığı için (route reuse), oy durumu her deal() değişiminde
    // o ürüne özel yeniden okunmalı.
    effect(() => {
      const productId = this.deal().productId;
      if (!this.isBrowser) {
        this.votedHelpful.set(null);
        return;
      }
      const stored = localStorage.getItem(`product-vote-${productId}`);
      this.votedHelpful.set(stored === 'yes' ? true : stored === 'no' ? false : null);
    });
  }

  // İlk ve son X ekseni etiketi tam kenara ortalanınca (-translate-x-1/2)
  // yarısı konteynerin dışına taşıp kesiliyordu (mobilde özellikle belirgin,
  // dar viewport'ta kenar payı daha az) — uçlardaki etiketleri kenara
  // ortalamak yerine kenara yaslıyoruz, ortadakiler ortalı kalıyor.
  protected xAxisLabelAlignClass(x: number): string {
    if (x <= 0) return 'left-0';
    if (x >= CHART_WIDTH) return '-translate-x-full';
    return '-translate-x-1/2';
  }

  protected toggleWatchForm(): void {
    this.watchFormOpen.update((open) => !open);
    this.watchStatusMessage.set(null);
  }

  protected onWatchSubmit(): void {
    const email = this.watchEmail().trim();
    if (!email) return;

    this.watchSubmitting.set(true);
    this.watchService.watch(this.deal().productId, email).subscribe({
      next: (result) => {
        this.watchStatusMessage.set(result.message);
        this.watchEmail.set('');
        this.watchSubmitting.set(false);
      },
      error: () => {
        this.watchStatusMessage.set('Bir şeyler ters gitti, tekrar dener misin?');
        this.watchSubmitting.set(false);
      },
    });
  }

  protected vote(helpful: boolean): void {
    if (this.votedHelpful() !== null) return;

    const productId = this.deal().productId;
    this.votedHelpful.set(helpful);
    if (this.isBrowser) {
      localStorage.setItem(`product-vote-${productId}`, helpful ? 'yes' : 'no');
    }
    this.productFeedbackService.vote(productId, helpful).subscribe();
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
        this.points.set(this.dedupeSameDaySamePrice(history.points));
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

  // Tarama günde birkaç kez çalıştığı için aynı gün içinde fiyat hiç
  // değişmemişse birden fazla nokta birikiyordu — hover'da art arda aynı
  // günü/fiyatı gösteren anlamsız duraklara yol açıyordu (saat/dakika
  // göstermiyoruz, sadece gün). Aynı gün + aynı fiyat olan ardışık
  // noktaları tek noktaya indiriyoruz; fiyat o gün içinde değiştiyse
  // (gerçek bir bilgi) ikisi de kalıyor. Sadece grafiğin çizdiği/hover
  // ettiği noktalar etkileniyor — En Düşük/En Yüksek/Şu Anki Fiyat
  // kutuları hâlâ ham backend verisinden (history.minPrice vb.) geliyor.
  // Aynı gün içinde (dedupe sonrası) başka bir nokta daha kaldıysa, bu
  // gerçek bir gün-içi fiyat değişikliği demektir — sadece tarih göstermek
  // hangi nokta hangi an olduğunu ayırt ettirmiyor, saat de ekleniyor.
  private tooltipDateLabel(points: { price: number; scrapedAt: string }[], idx: number): string {
    const day = axisDateFormatter.format(new Date(points[idx].scrapedAt));
    const sameDayCount = points.filter((p) => axisDateFormatter.format(new Date(p.scrapedAt)) === day).length;
    const formatter = sameDayCount > 1 ? tooltipDateTimeFormatter : tooltipDateFormatter;
    return formatter.format(new Date(points[idx].scrapedAt));
  }

  private dedupeSameDaySamePrice(points: { price: number; scrapedAt: string }[]): { price: number; scrapedAt: string }[] {
    const result: { price: number; scrapedAt: string }[] = [];

    for (const point of points) {
      const prev = result[result.length - 1];
      const sameDay = prev && axisDateFormatter.format(new Date(prev.scrapedAt)) === axisDateFormatter.format(new Date(point.scrapedAt));

      if (prev && sameDay && prev.price === point.price) {
        result[result.length - 1] = point;
      } else {
        result.push(point);
      }
    }

    return result;
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

    const candidates = Array.from({ length: AXIS_LABEL_COUNT }, (_, i) => {
      const fraction = i / (AXIS_LABEL_COUNT - 1);
      const t = minTime + (maxTime - minTime) * fraction;
      return { x: fraction * CHART_WIDTH, label: axisDateFormatter.format(new Date(t)) };
    });

    // Veri aralığı kısayken (ör. fiyat takibi daha birkaç gün sürdüğünde)
    // eşit zaman aralıklı 5 nokta aynı takvim gününe denk gelip aynı
    // etiketi (ör. "11 Ağu") art arda birden fazla kez gösterebiliyordu —
    // ardışık aynı etiketleri tekilleştiriyoruz.
    return candidates.filter((c, i) => i === 0 || c.label !== candidates[i - 1].label);
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

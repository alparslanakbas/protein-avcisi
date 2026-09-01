import { DOCUMENT, DecimalPipe, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { StoreLinkTargetDirective } from '../core/store-link-target.directive';
import { brandSlug } from '../core/brand-slug';
import { dedupeSameDaySamePrice, hoverAlign, nearestPointIndex, tooltipDateLabel } from '../core/chart-hover';
import { buildProductFacts } from '../core/product-facts';
import { buildProductNarrative } from '../core/product-narrative';
import { canonicalOrigin } from '../core/canonical-link';
import { Deal } from '../core/deal.model';
import { displayName } from '../core/display-name';
import { FavoritesService } from '../core/favorites.service';
import { friendlyErrorMessage } from '../core/friendly-error-message';
import { PricePoint } from '../core/price-history.model';
import { PriceHistoryService } from '../core/price-history.service';
import { ProductFeedbackService } from '../core/product-feedback.service';
import { formatRelativeTime } from '../core/relative-time';
import { slugify } from '../core/slugify';
import { buildAreaPath, buildLinePath, toCoordinates } from '../core/spark-chart';
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

// timeZone sabit Europe/Istanbul — kullanıcının kendi cihaz saat dilimine
// bırakılırsa (ör. yurt dışından erişim, ya da SSR'ın sunucu saat dilimi
// tarayıcıdan farklıysa) aynı fiyat noktası farklı ziyaretçilere farklı
// "gün"e ait gösterilebilirdi. Gerçek bir örnek: ProteinOcean'ın ilk
// taraması UTC 21:10'da olmuştu — bu, tarayıcı yerel saatine bırakılan
// eski davranışta bazı saat dilimlerinde "11 Ağustos", TR saatinde ise
// "10 Ağustos"un son dakikaları oluyordu; sabitleme bu tutarsızlığı
// ortadan kaldırıyor (site zaten sadece TR pazarına hizmet ediyor).
const axisDateFormatter = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', timeZone: 'Europe/Istanbul' });
const tooltipDateFormatter = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Europe/Istanbul' });
// Aynı gün içinde fiyat gerçekten değiştiyse (dedupeSameDaySamePrice sonrası
// hâlâ aynı güne ait birden fazla nokta kaldıysa) sadece tarih yetersiz
// kalıyor — o durumda saat de eklenip hangi anda değiştiği gösteriliyor.
const tooltipDateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'Europe/Istanbul',
});

@Component({
  selector: 'app-product-modal',
  imports: [StoreLinkTargetDirective, DecimalPipe, ShareButton, RouterLink, FormsModule],
  templateUrl: './product-modal.html',
})
export class ProductModal {
  // Marka bağlantısı için: toLowerCase() boşluklu/Türkçe harfli marka
  // adlarını adrese olduğu gibi taşıyıp kanonikten sapan kopya üretiyordu.
  protected readonly brandSlug = brandSlug;

  protected readonly displayName = displayName;
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly watchService = inject(WatchService);
  private readonly productFeedbackService = inject(ProductFeedbackService);
  private readonly favoritesService = inject(FavoritesService);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly deal = input.required<Deal>();
  readonly closed = output<void>();

  protected readonly shareUrl = computed(
    () => `${canonicalOrigin(this.document)}/urun/${this.deal().productId}/${slugify(this.deal().productName)}`,
  );

  protected readonly reviewLink = computed(() => ['/urun-inceleme', this.deal().productId, slugify(this.deal().productName)]);

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
  protected readonly points = signal<PricePoint[]>([]);
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

  // Favoriler — hesap gerektirmiyor, ilk eklemede e-posta isteniyor
  // (token localStorage'a kaydediliyor), sonraki ürünlerde tekrar
  // sorulmuyor. "favorited" oturum bazlı iyimser (optimistic) bir
  // durum — sayfa yenilenince sıfırlanır, tekrar tıklamak zararsız
  // (backend zaten aynı favoriyi ikinci kez eklemiyor).
  protected readonly favorited = signal(false);
  protected readonly favoriteFormOpen = signal(false);
  protected readonly favoriteEmail = signal('');
  protected readonly favoriteSubmitting = signal(false);
  protected readonly favoriteErrorMessage = signal<string | null>(null);
  protected readonly favoriteStatusMessage = signal<string | null>(null);

  protected readonly coordinates = computed(() =>
    toCoordinates(this.points(), this.minPrice(), this.maxPrice(), {
      width: CHART_WIDTH,
      height: CHART_HEIGHT,
      paddingY: CHART_PADDING_Y,
    }),
  );
  protected readonly chartAreaPath = computed(() => buildAreaPath(this.coordinates(), CHART_HEIGHT));
  protected readonly chartLinePath = computed(() => buildLinePath(this.coordinates()));
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
    const align = hoverAlign(x, CHART_WIDTH);

    return {
      x,
      y,
      align,
      price: pts[idx].price,
      dateLabel: tooltipDateLabel(pts, idx),
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

  // "Bu ürün son X günde Y kez indirime girdi" — ham veri yerine bir
  // içgörü. points() zaten dedupeSameDaySamePrice'tan geçtiği için
  // (aynı gün aynı fiyat tekrarları elenmiş) art arda gerçek fiyat
  // düşüşlerini saymak yeterli. 0 ise hiç gösterilmiyor (boş/soğuk bir
  // "0 kez" mesajı yerine sessiz kalmak tercih edildi).
  protected readonly discountEventCount = computed(() => {
    const pts = this.points();
    let count = 0;
    for (let i = 1; i < pts.length; i++) {
      if (pts[i].price < pts[i - 1].price) count++;
    }
    return count;
  });

  // Markanın kendi açıklama metni artık BURADA GÖSTERİLMİYOR — yerine
  // tamamen kendi ölçümlerimizden türeyen bir bilgi listesi basılıyor.
  // Gerekçe için core/product-facts.ts'teki nota bak.
  protected readonly productFacts = computed(() =>
    buildProductFacts(this.deal(), this.discountEventCount()),
  );

  // Kendi ölçümlerimizden üretilen anlatı. Bilgi listesi taranabilirlik için
  // duruyor; bu bölüm ise sayfanın ayırt edici metnini sağlıyor (bkz.
  // core/product-narrative.ts'teki gerekçe).
  protected readonly narrative = computed(() =>
    buildProductNarrative(this.deal(), this.discountEventCount()),
  );

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

    // "favorited" da deal() bazlı sıfırlanmalı — aksi halde bir önceki
    // üründeki "Favorilere Eklendi" durumu yeni ürüne sızardı.
    effect(() => {
      this.deal();
      this.favorited.set(false);
      this.favoriteFormOpen.set(false);
      this.favoriteErrorMessage.set(null);
      this.favoriteStatusMessage.set(null);
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
      error: (err) => {
        this.watchStatusMessage.set(friendlyErrorMessage(err));
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

  protected toggleFavorite(): void {
    if (this.favorited()) return;

    // Zaten bir token varsa (daha önce başka bir üründe e-posta girilmiş)
    // tekrar sormadan direkt ekle — sadece ilk seferde form açılıyor.
    if (this.favoritesService.getToken()) {
      this.addFavorite();
    } else {
      this.favoriteFormOpen.set(true);
    }
  }

  protected onFavoriteSubmit(): void {
    this.addFavorite(this.favoriteEmail().trim());
  }

  private addFavorite(email?: string): void {
    if (email !== undefined && !email) return;

    this.favoriteSubmitting.set(true);
    this.favoriteErrorMessage.set(null);
    this.favoritesService.add(this.deal().productId, email).subscribe({
      next: (result) => {
        this.favoritesService.saveToken(result.token);
        this.favorited.set(true);
        this.favoriteFormOpen.set(false);
        this.favoriteEmail.set('');
        this.favoriteSubmitting.set(false);
        // Token null ama bu cihaz zaten önceden token almışsa (result.token
        // null olması normal — bkz. token null dönme kuralı) hiçbir ek
        // mesaj gerekmiyor, liste zaten doğru çalışacak. recoverySent true
        // ise bu cihazda hiç token yoktu, e-posta gönderildi.
        this.favoriteStatusMessage.set(
          result.recoverySent
            ? 'Favorilere eklendi! Bu cihazda listeni görebilmek için e-postana gönderdiğimiz linke tıkla (farklı bir tarayıcı kullanıyorsan oradan da ayrıca tıklaman gerekir).'
            : null,
        );
      },
      error: (err) => {
        this.favoriteErrorMessage.set(friendlyErrorMessage(err));
        this.favoriteSubmitting.set(false);
      },
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
    this.updateHoverFromClientX(event.currentTarget as SVGSVGElement, event.clientX);
  }

  protected onChartMouseLeave(): void {
    this.hoverIndex.set(null);
  }

  // Mobilde mousemove tetiklenmiyor (dokunma, gerçek bir sürükleme değil
  // tek tek tap olarak algılanıyordu) — Akakçe'deki gibi parmağı grafik
  // üzerinde gezdirince noktanın kesintisiz takip etmesi için touchstart/
  // touchmove'a ayrıca bağlandı, aynı en-yakın-nokta hesabını kullanıyor.
  // touchend'de bilinçli olarak hoverIndex sıfırlanmıyor — parmak kalkınca
  // son değer görünür kalıyor, kaybolmuyor.
  protected onChartTouchMove(event: TouchEvent): void {
    const touch = event.touches[0];
    if (!touch) return;
    // Grafik içindeyken sayfanın kaydırılmasını (scroll) engelliyoruz —
    // aksi halde parmak grafikte gezinirken modal da kayardı.
    event.preventDefault();
    this.updateHoverFromClientX(event.currentTarget as SVGSVGElement, touch.clientX);
  }

  private updateHoverFromClientX(svg: SVGSVGElement, clientX: number): void {
    const idx = nearestPointIndex(svg, clientX, this.coordinates(), CHART_WIDTH);
    if (idx !== null) this.hoverIndex.set(idx);
  }

  private load(productId: number, days: number): void {
    this.loading.set(true);
    this.error.set(null);

    this.priceHistoryService.get(productId, days).subscribe({
      next: (history) => {
        this.points.set(dedupeSameDaySamePrice(history.points));
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
  private buildXAxisLabels(points: PricePoint[]): { x: number; label: string }[] {
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
}

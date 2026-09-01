import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { productPath, shouldHandleInApp } from '../core/product-link';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { FavoritesService } from '../core/favorites.service';
import { LoadErrorInfo, describeLoadError, friendlyErrorMessage } from '../core/friendly-error-message';
import { PageMetaService } from '../core/page-meta.service';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { ProductModal } from '../product-modal/product-modal';
import { SiteHeader } from '../site-header/site-header';

// Hız sınırına takılınca kaç kez sessizce tekrar denenecek. İkiden fazlası
// anlamsız: sorun geçici değilse kullanıcıya durumu göstermek daha dürüst.
const MAX_AUTO_RETRY = 2;

@Component({
  selector: 'app-favorites-page',
  imports: [DecimalPipe, RouterLink, ProductModal, SiteHeader, FormsModule],
  templateUrl: './favorites-page.html',
  styleUrl: './favorites-page.css',
})
export class FavoritesPage implements OnInit, OnDestroy {
  protected readonly displayName = displayName;
  private readonly favoritesService = inject(FavoritesService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly pageMeta = inject(PageMetaService);
  private readonly metaService = inject(Meta);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly dealsService = inject(DealsService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private autoRetryCount = 0;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly favorites = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<LoadErrorInfo | null>(null);
  protected readonly hasToken = signal(false);
  protected readonly discountedCount = computed(() => this.favorites().filter((deal) => deal.discountPercent > 0).length);
  protected readonly lowCount = computed(() => this.favorites().filter((deal) => deal.isAtThirtyDayLow).length);
  protected readonly opportunityCount = computed(
    () => this.favorites().filter((deal) => deal.discountPercent > 0 || deal.isAtThirtyDayLow).length,
  );
  protected readonly normalCount = computed(() => this.favorites().length - this.opportunityCount());
  protected readonly opportunityRate = computed(() => {
    const total = this.favorites().length;
    return total === 0 ? 0 : Math.round((this.opportunityCount() / total) * 100);
  });

  // bkz. category-page.ts'teki aynı gerekçe.
  protected readonly selectedDeal = signal<Deal | null>(null);

  // Favori listesi kurtarma — token bu cihazda yoksa e-posta girip link
  // isteyebiliyor (bkz. FavoritesService.recover, backend'deki
  // FavoriteService.SendRecoveryEmailAsync). product-modal.ts'teki
  // watch/favorite inline form desenleriyle aynı signal yapısı.
  protected readonly recoverEmail = signal('');
  protected readonly recoverSubmitting = signal(false);
  protected readonly recoverStatusMessage = signal<string | null>(null);

  ngOnInit(): void {
    // Başlık ve açıklama "Takip listem" demeli: sayfanın h1'i, üst menü,
    // footer ve mobil sekme çubuğu bu adı kullanıyor. Tasarım turunda kavram
    // yeniden adlandırıldı ama meta tarafı "Favorilerim"de kalmıştı — arama
    // sonucunda kullanıcının sitede gördüğünden başka bir isim çıkıyordu.
    //
    // ADRES bilinçli olarak /favorilerim kalıyor: dışarıda verilmiş
    // bağlantılar ve e-postadaki kurtarma linki bu adrese işaret ediyor,
    // değiştirmenin SEO/kullanıcı tarafında hiçbir karşılığı yok (sayfa
    // zaten noindex).
    this.pageMeta.set({
      title: 'Takip listem | ProteinAvcısı',
      description: 'Takip listene eklediğin ürünlerin güncel fiyatlarını ve fiyat düşüşlerini buradan izle.',
      canonicalPath: '/favorilerim',
    });
    // Kişiye özel içerik (localStorage token'ına bağlı) — arama motoru
    // botları için anlamsız/boş görünür, indekslenmesin diye noindex.
    this.metaService.updateTag({ name: 'robots', content: 'noindex' });

    // E-postadan tıklanan kurtarma linki (?recover=TOKEN) — token'ı bu
    // cihaza kaydedip URL'den temizliyoruz (tarayıcı geçmişinde/paylaşımda
    // token açıkta kalmasın diye). Sadece ilk yüklemede kontrol etmek
    // yeterli, bu yüzden snapshot kullanılıyor (queryParamMap aboneliği
    // aşağıda ayrıca ?urun= için sürüyor).
    // KRİTİK: sadece tarayıcıda çalıştırılmalı. router.navigate() SSR
    // sırasında da çağrılırsa Angular Universal bunu gerçek bir HTTP 302
    // yönlendirmesine çeviriyor — tarayıcı hiç HTML/JS almadan doğrudan
    // /favorilerim'e (recover parametresi silinmiş halde) yönlendiriliyor,
    // saveToken() ise sunucuda no-op olduğu için token hiçbir yere
    // kaydolmadan kayboluyor. Gerçek bir kullanıcı testinde bulundu —
    // yerel ng serve (SSR'sız) testinde bu hiç ortaya çıkmamıştı.
    const recoverToken = this.route.snapshot.queryParamMap.get('recover');
    if (recoverToken && this.isBrowser) {
      this.favoritesService.saveToken(recoverToken);
      this.router.navigate([], { relativeTo: this.route, queryParams: { recover: null }, queryParamsHandling: 'merge', replaceUrl: true });
    }

    this.hasToken.set(!!this.favoritesService.getToken());
    this.loadFavorites();

    this.route.queryParamMap.subscribe((params) => {
      const idParam = params.get('urun');
      if (!idParam) {
        this.selectedDeal.set(null);
        return;
      }

      const id = Number(idParam);
      const alreadyLoaded = this.favorites().find((d) => d.productId === id);
      if (alreadyLoaded) {
        this.selectedDeal.set(alreadyLoaded);
        return;
      }

      this.dealsService.getProductById(id).subscribe({
        next: (deal) => this.selectedDeal.set(deal),
        error: () => this.selectedDeal.set(null),
      });
    });
  }

  private loadFavorites(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.favoritesService.list().subscribe({
      next: (deals) => {
        this.favorites.set(deals);
        this.loadError.set(null);
        this.loading.set(false);
        this.autoRetryCount = 0;
      },
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.favoritesService.signOut();
          this.hasToken.set(false);
          this.favorites.set([]);
          this.recoverStatusMessage.set(
            'Bu cihazdaki liste bağlantısı artık geçerli değil. E-postanla listeni yeniden açabilirsin.',
          );
          this.loading.set(false);
          return;
        }

        // 429 = hız sınırı (Cloudflare). Kalıcı bir arıza DEĞİL, birkaç
        // saniye içinde kendiliğinden geçiyor; "Bağlantı sorunu" ekranı
        // göstermek yanıltıcı olurdu. Yükleniyor durumunda kalıp kısa bir
        // süre sonra sessizce tekrar deniyoruz.
        //
        // Retry-After başlığı çapraz kökenli yanıtta JS'ye açık olmayabilir
        // (Access-Control-Expose-Headers'a bağlı), bu yüzden okunamazsa
        // gözlenen değere (10 sn) düşülüyor.
        if (error instanceof HttpErrorResponse && error.status === 429 && this.autoRetryCount < MAX_AUTO_RETRY) {
          this.autoRetryCount++;
          const retryAfter = Number(error.headers?.get('Retry-After'));
          const waitMs = (Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : 10) * 1000;
          this.clearRetryTimer();
          this.retryTimer = setTimeout(() => this.loadFavorites(), waitMs);
          return;
        }

        this.loadError.set(describeLoadError(error));
        this.loading.set(false);
      },
    });
  }

  protected retryLoad(): void {
    // Kullanıcı bilinçli olarak bastıysa otomatik tekrar hakkı sıfırlanıyor.
    this.autoRetryCount = 0;
    this.clearRetryTimer();
    this.loadFavorites();
  }

  private clearRetryTimer(): void {
    if (this.retryTimer !== null) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
  }

  // Sayfadan ayrılınca bekleyen tekrar denemesi iptal ediliyor; yoksa
  // kullanıcı başka bir sayfadayken gereksiz bir istek gidiyor.
  ngOnDestroy(): void {
    this.clearRetryTimer();
  }

  // Listeyi yalnızca bu tarayıcıdan ayırır — sunucudaki favoriler duruyor,
  // kurtarma bağlantısıyla geri alınabiliyor. Sayfa, token'ı olmayan
  // ziyaretçiye gösterdiği "e-postana bağlantı gönderelim" formuna dönüyor.
  protected signOut(): void {
    this.favoritesService.signOut();
    this.hasToken.set(false);
    this.favorites.set([]);
    this.recoverStatusMessage.set(null);
  }

  protected submitRecover(): void {
    const email = this.recoverEmail().trim();
    if (!email) return;

    this.recoverSubmitting.set(true);
    this.favoritesService.recover(email).subscribe({
      next: (result) => {
        this.recoverStatusMessage.set(result.message);
        this.recoverSubmitting.set(false);
      },
      error: (err) => {
        this.recoverStatusMessage.set(friendlyErrorMessage(err));
        this.recoverSubmitting.set(false);
      },
    });
  }

  protected removeFavorite(deal: Deal): void {
    this.favoritesService.remove(deal.productId).subscribe(() => {
      this.favorites.update((list) => list.filter((d) => d.productId !== deal.productId));
    });
  }

  // Kart/satır bağlantıları gerçek <a href> olmak zorunda (bkz.
  // core/product-link.ts). Bu sayfalarda modal, ürün sayfasına gitmeden
  // ?urun= parametresiyle açılıyor — bu yüzden RouterLink yerine gerçek bir
  // href + kontrollü tıklama kullanılıyor: bot kanonik ürün adresini görüyor,
  // kullanıcı ise sayfadan ayrılmadan modalı açıyor.
  protected productPath(deal: Deal): string {
    return productPath(deal);
  }

  protected onProductClick(event: MouseEvent, deal: Deal): void {
    // Satırın/kartın kendi tıklama işleyicisi de varsa iki kez tetiklenmesin.
    event.stopPropagation();
    if (!shouldHandleInApp(event)) return;
    event.preventDefault();
    this.openDeal(deal);
  }

  protected openDeal(deal: Deal): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { urun: deal.productId }, queryParamsHandling: 'merge' });
  }

  protected closeDeal(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { urun: null }, queryParamsHandling: 'merge' });
  }

  protected lastCheckedText(deal: Deal): string {
    return formatRelativeTime(deal.scrapedAt);
  }

  protected goToStoreUrl(deal: Deal): string {
    return this.priceHistoryService.goToStoreUrl(deal.productId, deal.storeUrl);
  }

  /** Mağaza tıklamasını sayar; bağlantı doğrudan mağazaya gittiği için
   *  sayacı artık /go/{id} artıramıyor (bkz. PriceHistoryService). */
  protected magazaTiklamasi(productId: number): void {
    this.priceHistoryService.trackStoreClick(productId);
  }
}

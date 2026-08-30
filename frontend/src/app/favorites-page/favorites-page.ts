import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { productPath, shouldHandleInApp } from '../core/product-link';
import { DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { FavoritesService } from '../core/favorites.service';
import { friendlyErrorMessage } from '../core/friendly-error-message';
import { PageMetaService } from '../core/page-meta.service';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { ProductModal } from '../product-modal/product-modal';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-favorites-page',
  imports: [DecimalPipe, RouterLink, ProductModal, SiteHeader, FormsModule],
  templateUrl: './favorites-page.html',
  styleUrl: './favorites-page.css',
})
export class FavoritesPage implements OnInit {
  protected readonly displayName = displayName;
  private readonly favoritesService = inject(FavoritesService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly pageMeta = inject(PageMetaService);
  private readonly metaService = inject(Meta);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly dealsService = inject(DealsService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly favorites = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
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
    this.pageMeta.set({
      title: 'Favorilerim | ProteinAvcısı',
      description: 'Favorilerine eklediğin ürünlerin güncel fiyatlarını buradan takip et.',
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
    this.loadError.set(false);
    this.favoritesService.list().subscribe({
      next: (deals) => {
        this.favorites.set(deals);
        this.loadError.set(false);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  protected retryLoad(): void {
    this.loadFavorites();
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

  protected goToStoreUrl(productId: number): string {
    return this.priceHistoryService.goToStoreUrl(productId);
  }
}

import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { FavoritesService } from '../core/favorites.service';
import { PageMetaService } from '../core/page-meta.service';
import { PriceHistoryService } from '../core/price-history.service';
import { formatRelativeTime } from '../core/relative-time';
import { ProductModal } from '../product-modal/product-modal';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-favorites-page',
  imports: [DecimalPipe, RouterLink, ProductModal, SiteHeader, FormsModule],
  templateUrl: './favorites-page.html',
})
export class FavoritesPage implements OnInit {
  private readonly favoritesService = inject(FavoritesService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly pageMeta = inject(PageMetaService);
  private readonly metaService = inject(Meta);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly dealsService = inject(DealsService);

  protected readonly favorites = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly hasToken = signal(false);

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
    const recoverToken = this.route.snapshot.queryParamMap.get('recover');
    if (recoverToken) {
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
    this.favoritesService.list().subscribe({
      next: (deals) => {
        this.favorites.set(deals);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
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
      error: () => {
        this.recoverStatusMessage.set('Bir şeyler ters gitti, tekrar dener misin?');
        this.recoverSubmitting.set(false);
      },
    });
  }

  protected removeFavorite(deal: Deal): void {
    this.favoritesService.remove(deal.productId).subscribe(() => {
      this.favorites.update((list) => list.filter((d) => d.productId !== deal.productId));
    });
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

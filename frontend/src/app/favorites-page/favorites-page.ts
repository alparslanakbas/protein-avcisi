import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { FavoritesService } from '../core/favorites.service';
import { PageMetaService } from '../core/page-meta.service';

@Component({
  selector: 'app-favorites-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './favorites-page.html',
})
export class FavoritesPage implements OnInit {
  private readonly favoritesService = inject(FavoritesService);
  private readonly router = inject(Router);
  private readonly pageMeta = inject(PageMetaService);
  private readonly metaService = inject(Meta);

  protected readonly favorites = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly hasToken = signal(false);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Favorilerim | ProteinAvcısı',
      description: 'Favorilerine eklediğin ürünlerin güncel fiyatlarını buradan takip et.',
      canonicalPath: '/favorilerim',
    });
    // Kişiye özel içerik (localStorage token'ına bağlı) — arama motoru
    // botları için anlamsız/boş görünür, indekslenmesin diye noindex.
    this.metaService.updateTag({ name: 'robots', content: 'noindex' });

    this.hasToken.set(!!this.favoritesService.getToken());
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

  protected removeFavorite(deal: Deal): void {
    this.favoritesService.remove(deal.productId).subscribe(() => {
      this.favorites.update((list) => list.filter((d) => d.productId !== deal.productId));
    });
  }

  protected goToProduct(deal: Deal): void {
    this.router.navigate(['/urun', deal.productId]);
  }
}

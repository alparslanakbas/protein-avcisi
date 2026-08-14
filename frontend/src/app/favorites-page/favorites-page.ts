import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';

import { setCanonicalLink } from '../core/canonical-link';
import { Deal } from '../core/deal.model';
import { FavoritesService } from '../core/favorites.service';

@Component({
  selector: 'app-favorites-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './favorites-page.html',
})
export class FavoritesPage implements OnInit {
  private readonly favoritesService = inject(FavoritesService);
  private readonly router = inject(Router);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  protected readonly favorites = signal<Deal[]>([]);
  protected readonly loading = signal(true);
  protected readonly hasToken = signal(false);

  ngOnInit(): void {
    this.titleService.setTitle('Favorilerim | ProteinAvcısı');
    this.metaService.updateTag({ name: 'description', content: 'Favorilerine eklediğin ürünlerin güncel fiyatlarını buradan takip et.' });
    // Kişiye özel içerik (localStorage token'ına bağlı) — arama motoru
    // botları için anlamsız/boş görünür, indekslenmesin diye noindex.
    this.metaService.updateTag({ name: 'robots', content: 'noindex' });
    setCanonicalLink(this.document, '/favorilerim');

    this.hasToken.set(!!this.favoritesService.getToken());
    this.favoritesService.list().subscribe({
      next: (deals) => {
        this.favorites.set(deals);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
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

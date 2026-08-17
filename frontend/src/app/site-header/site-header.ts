import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { CATEGORY_LABELS } from '../core/category-labels';
import { DealsService } from '../core/deals.service';
import { FavoritesService } from '../core/favorites.service';
import { ThemePreference, ThemeService } from '../core/theme.service';

// Ana sayfa (deals-list) dışındaki tüm sayfalarda (kategori, marka, takip
// listem, karşılaştırma, rehber) kullanılan paylaşılan nav — logo, tema
// toggle'ı ve takip listesi rozetini tek yerde tutuyor. Ana sayfa kendi
// arama kutusuna sahip özel nav'ını koruyor (bu bileşeni kullanmıyor),
// o yüzden burada arama yok.
@Component({
  selector: 'app-site-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './site-header.html',
})
export class SiteHeader implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly favoritesService = inject(FavoritesService);
  protected readonly theme = inject(ThemeService);

  // Kullanıcı geri bildirimi: kategori sayfalarına footer'dan başka
  // erişimi olmayan biri onları neredeyse hiç görmüyordu — nav'a
  // gerçek bir "Kategoriler" açılır menüsü eklendi (ayrı bir index
  // sayfası kurmaya gerek kalmadan, /api/filters'tan gelen gerçek
  // kategori listesiyle).
  protected readonly categories = signal<{ slug: string; label: string }[]>([]);
  protected readonly categoriesOpen = signal(false);
  protected readonly favoritesCount = signal(0);

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.categories.set(options.categories.map((slug) => ({ slug, label: CATEGORY_LABELS[slug] ?? slug })));
    });
    this.favoritesService.list().subscribe((list) => this.favoritesCount.set(list.length));
  }

  protected toggleCategories(): void {
    this.categoriesOpen.update((open) => !open);
  }

  protected closeCategories(): void {
    this.categoriesOpen.set(false);
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
  }
}

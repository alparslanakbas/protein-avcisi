import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { BODY_CALCULATORS } from '../core/body-calculators';
import { CATEGORY_LABELS } from '../core/category-labels';
import { DealsService } from '../core/deals.service';
import { FavoritesService } from '../core/favorites.service';
import {
  CALCULATOR_ICON_PATHS,
  CATEGORY_ICON_PATHS,
  DEFAULT_CATEGORY_ICON,
  calculatorIconPath,
  calculatorPhosphorIcon,
  categoryPhosphorIcon,
} from '../core/nav-icons';
import { SUPPLEMENT_DOSAGES } from '../core/supplement-dosages';
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
  protected readonly categories = signal<{ slug: string; label: string; iconPath: string; iconClass: string }[]>([]);
  protected readonly categoriesOpen = signal(false);

  // Araç sayısı birden fazlaya çıkınca "Hesaplama" da düz link olmaktan
  // çıkıp Kategoriler'le aynı dropdown desenine geçti. İkonlar kullanıcı
  // geri bildirimiyle eklendi: dropdown'lar sadece düz metindi.
  protected readonly calculatorsOpen = signal(false);
  protected readonly calculators = [
    {
      path: '/hesaplama/protein-ihtiyaci',
      label: 'Günlük Protein İhtiyacı',
      iconPath: CALCULATOR_ICON_PATHS.plate,
      iconClass: calculatorPhosphorIcon('protein-ihtiyaci'),
    },
    ...BODY_CALCULATORS.map((c) => ({
      path: `/hesaplama/${c.slug}`,
      label: c.name,
      iconPath: calculatorIconPath(c.slug),
      iconClass: calculatorPhosphorIcon(c.slug),
    })),
    ...SUPPLEMENT_DOSAGES.map((s) => ({
      path: `/hesaplama/${s.slug}`,
      label: `${s.name} Dozu`,
      iconPath: CALCULATOR_ICON_PATHS.capsule,
      iconClass: calculatorPhosphorIcon(s.slug),
    })),
  ];
  // Servisteki paylaşılan signal'e doğrudan referans — favori eklenince/
  // çıkarılınca (bu sayfadan ya da başka bir sayfadan) otomatik güncellenir.
  protected readonly favoritesCount = this.favoritesService.count;

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.categories.set(
        options.categories.map((slug) => ({
          slug,
          label: CATEGORY_LABELS[slug] ?? slug,
          iconPath: CATEGORY_ICON_PATHS[slug] ?? DEFAULT_CATEGORY_ICON,
          iconClass: categoryPhosphorIcon(slug),
        })),
      );
    });
    this.favoritesService.list().subscribe();
  }

  protected toggleCategories(): void {
    this.categoriesOpen.update((open) => !open);
  }

  protected closeCategories(): void {
    this.categoriesOpen.set(false);
  }

  protected toggleCalculators(): void {
    this.calculatorsOpen.update((open) => !open);
  }

  protected closeCalculators(): void {
    this.calculatorsOpen.set(false);
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
  }
}

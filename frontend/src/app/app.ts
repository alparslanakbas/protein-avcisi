import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { CATEGORY_LABELS } from './core/category-labels';
import { DealsService } from './core/deals.service';
import { ComparisonBar } from './comparison-bar/comparison-bar';
import { CookieConsentBanner } from './cookie-consent-banner/cookie-consent-banner';
import { MobileTabBar } from './mobile-tab-bar/mobile-tab-bar';
import { NewsletterSignup } from './newsletter-signup/newsletter-signup';
import { UpdateBanner } from './update-banner/update-banner';

// Route ağacının en derinindeki component referansını buluyor —
// DealsRouteReuseStrategy'nin "aynı component mi" kontrolüyle aynı mantık.
function leafComponent(snapshot: ActivatedRouteSnapshot): unknown {
  let current = snapshot;
  while (current.firstChild) current = current.firstChild;
  return current.routeConfig?.component ?? null;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, NewsletterSignup, CookieConsentBanner, MobileTabBar, ComparisonBar, UpdateBanner],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private lastLeafComponent: unknown = undefined;

  protected readonly currentYear = new Date().getFullYear();
  protected readonly brands = signal<string[]>([]);
  protected readonly categories = signal<{ slug: string; label: string }[]>([]);

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.brands.set(options.brands);
      this.categories.set(options.categories.map((slug) => ({ slug, label: CATEGORY_LABELS[slug] ?? slug })));
    });

    // Kullanıcı geri bildirimi: footer'daki bir linke (ör. Rehber, Kategoriler)
    // tıklayınca sayfa değişiyor ama scroll konumu sayfanın altında kalıyor —
    // SPA navigasyonu tarayıcının varsayılan "sayfa değişince en üste git"
    // davranışını miras almıyor. Angular'ın hazır `withInMemoryScrolling`
    // ayarı bunu HER navigasyonda (ör. kategori/marka sayfasındaki ?urun=
    // query param'ıyla açılan ürün modalında da) tetikleyip modal
    // açılırken/kapanırken arka plan sayfasını istenmeden en üste
    // zıplatırdı — bu yüzden burada sadece gerçekten FARKLI bir sayfaya
    // (component'e) geçildiğinde en üste kaydırıyoruz; aynı sayfa içindeki
    // route-reuse navigasyonlarına (modal aç/kapa) dokunmuyoruz.
    if (this.isBrowser) {
      this.router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)).subscribe(() => {
        const current = leafComponent(this.router.routerState.snapshot.root);
        if (current !== this.lastLeafComponent) {
          window.scrollTo(0, 0);
        }
        this.lastLeafComponent = current;
      });
    }
  }
}

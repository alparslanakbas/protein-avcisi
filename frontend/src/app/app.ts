import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { brandSlug } from './core/brand-slug';
import { canonicalOrigin } from './core/canonical-link';
import { CATEGORY_LABELS } from './core/category-labels';
import { DealsService } from './core/deals.service';
import { upsertJsonLdScript } from './core/page-meta.service';
import { FOUNDER, SITE_NAME } from './core/site-identity';
import { ComparisonBar } from './comparison-bar/comparison-bar';
import { CookieConsentBanner } from './cookie-consent-banner/cookie-consent-banner';
import { MobileTabBar } from './mobile-tab-bar/mobile-tab-bar';
import { NewsletterSignup } from './newsletter-signup/newsletter-signup';
import { UpdateBanner } from './update-banner/update-banner';

// Route ağacının en derinindeki component referansını buluyor —
// DealsRouteReuseStrategy'nin "aynı component mi" kontrolüyle aynı mantık.
// ÖNEMLİ: current.routeConfig.component DEĞİL, current.component kullanılıyor —
// routeConfig.component sadece statik (eager) import edilen route'larda dolu;
// route bazlı lazy loading eklendikten sonra (loadComponent kullanan route'lar)
// bu alan hep undefined kalıyordu, iki farklı lazy sayfa arasında geçişte
// "undefined !== undefined" hep false çıkıp scroll hiç sıfırlanmıyordu (gerçek
// bir prod bug'ı, kullanıcı bildirdi). current.component ise Router'ın
// resolve ettiği gerçek sınıfı taşıyor, hem eager hem lazy route'larda dolu.
function leafComponent(snapshot: ActivatedRouteSnapshot): unknown {
  let current = snapshot;
  while (current.firstChild) current = current.firstChild;
  return current.component ?? null;
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
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private lastLeafComponent: unknown = undefined;
  private lastPath: string | null = null;

  protected readonly currentYear = new Date().getFullYear();
  // Marka adı boşluk ya da Türkçe harf taşıyabiliyor ("Torq Nutrition",
  // "Yeşilmarka"); toLowerCase() bunları adrese olduğu gibi taşıyıp
  // %20/%C5%9F içeren ikinci bir adres üretiyordu.
  protected readonly brandSlug = brandSlug;

  protected readonly brands = signal<string[]>([]);
  protected readonly categories = signal<{ slug: string; label: string }[]>([]);

  // Marka karşılaştırma sayfalarına (/karsilastir/:pair) footer'dan bir giriş
  // noktası — brand-page.ts'teki comparisonPairSlug ile aynı kanonik kural:
  // alfabetik sıralı, benzersiz ikili kombinasyonlar (hiq-vs-ssn gibi, hiç
  // ssn-vs-hiq yok — duplicate content'e düşmemek için).
  protected readonly comparisonPairs = computed(() => {
    const sorted = [...this.brands()].sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase()));
    const pairs: { slug: string; label: string }[] = [];
    for (let i = 0; i < sorted.length; i++) {
      for (let j = i + 1; j < sorted.length; j++) {
        pairs.push({
          slug: `${sorted[i].toLowerCase()}-vs-${sorted[j].toLowerCase()}`,
          label: `${sorted[i]} - ${sorted[j]}`,
        });
      }
    }
    return pairs;
  });

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.brands.set(options.brands);
      this.categories.set(options.categories.map((slug) => ({ slug, label: CATEGORY_LABELS[slug] ?? slug })));
    });

    // Organization + Person (kurucu) schema.org işaretlemesi — site
    // genelinde, sayfa navigasyonundan bağımsız, sadece bir kez eklenip
    // hiç kaldırılmıyor (deals-list.ts'teki ürün Product/FAQ JSON-LD'sinin
    // aksine, bu ikisi tüm sayfalarda sabit kalmalı). YMYL niteliğindeki
    // bir konuda (takviye/sağlık) yazar kimliği sinyali için — rakip
    // analizinde eksik olduğumuz, en yüksek etkili maddeydi.
    const origin = canonicalOrigin(this.document);
    upsertJsonLdScript(this.document, null, {
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: SITE_NAME,
      url: origin,
      logo: `${origin}/favicon.svg`,
      founder: {
        '@type': 'Person',
        name: FOUNDER.name,
        jobTitle: FOUNDER.jobTitle,
        url: FOUNDER.blogUrl,
        sameAs: [FOUNDER.linkedInUrl],
      },
    });
    upsertJsonLdScript(this.document, null, {
      '@context': 'https://schema.org',
      '@type': 'Person',
      name: FOUNDER.name,
      jobTitle: FOUNDER.jobTitle,
      url: FOUNDER.blogUrl,
      sameAs: [FOUNDER.linkedInUrl],
      worksFor: { '@type': 'Organization', name: SITE_NAME, url: origin },
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
        // Yalnızca component'e bakmak yetmiyordu: marka sayfasındaki "Diğer
        // Markalar" bağlantıları ya da kategori sayfasındaki "Diğer
        // Kategoriler" aynı component'te kaldığı için sayfa değişse bile
        // kaydırma konumu olduğu yerde duruyordu. Yol da karşılaştırılıyor.
        // Fragment de kesiliyor ('#' dahil), yalnızca '?' değil: router.url
        // fragment'i içeriyor, dolayısıyla sayfa içi bir bölüm bağlantısına
        // (#kvkk-haklari gibi) tıklamak burada "farklı sayfaya geçildi" gibi
        // görünüyordu ve aşağıdaki scrollTo(0,0) tarayıcının az önce yaptığı
        // bölüme kaydırmayı geri alıyordu. Kullanıcı bunu "ilk tıklama yukarı
        // atıyor, ikinci tıklama doğru yere götürüyor" olarak yaşadı —
        // ikincide yol artık değişmediği için sıfırlama çalışmıyordu.
        const path = this.router.url.split(/[?#]/)[0];
        const changed = current !== this.lastLeafComponent || path !== this.lastPath;
        // Tek istisna ürün modalı: ana sayfada modal açılıp kapanması '/' ile
        // '/urun/...' arasında gerçek bir yol değişimi olarak görünüyor ama
        // aslında aynı sayfanın üstündeki bir katman (bkz.
        // DealsRouteReuseStrategy). Burada kaydırırsak, sayfanın ortasındaki
        // bir ürüne tıklayıp modalı kapatan kişi kendini en başta bulurdu.
        // İstisna yalnızca aynı component içinde kalırken geçerli:
        // ürün modalından marka sayfasına geçmek gerçek bir sayfa
        // değişimi ve orada kaydırma sıfırlanmalı.
        const sameComponent = current === this.lastLeafComponent;
        const productModalNav =
          sameComponent && (path.startsWith('/urun/') || (this.lastPath?.startsWith('/urun/') ?? false));

        if (changed && !productModalNav) {
          window.scrollTo(0, 0);
        }
        this.lastLeafComponent = current;
        this.lastPath = path;
      });
    }
  }
}

import { Routes, UrlSegment } from '@angular/router';

import { DealsList } from './deals-list/deals-list';
import { BODY_CALCULATORS } from './core/body-calculators';

// Ana sayfa (DealsList) BİLİNÇLİ OLARAK eager (statik import) kaldı — hem SSR'ın
// ilk isteği hem de en sık ziyaret edilen route bu, lazy yapmak ilk yükte bir
// round-trip daha eklerdi. Diğer TÜM sayfalar `loadComponent` ile lazy —
// hiçbiri ilk ziyaretin kritik yolunda değil (kategori/marka/rehber/hesaplama/
// karşılaştırma/statik sayfalar), `withPreloading(PreloadAllModules)` sayesinde
// (bkz. app.config.ts) ana sayfa yüklendikten hemen sonra arka planda zaten
// indiriliyor olacaklar — kullanıcı tıkladığında ekstra bir gecikme olmuyor.
export const routes: Routes = [
  { path: '', component: DealsList },
  { path: 'urun/:id', component: DealsList },
  { path: 'urun/:id/:slug', component: DealsList },
  {
    path: 'marka/:brandSlug/indirim-kodu',
    loadComponent: () => import('./brand-page/brand-page').then((m) => m.BrandPage),
  },
  // Marka × kategori kesişimi ("hardline protein tozu fiyatları" gibi
  // aramalar için). SIRA ÖNEMLİ: 'indirim-kodu' bu satırdan ÖNCE tanımlı
  // olmalı, yoksa o da bir kategori slug'ı sanılır.
  {
    path: 'marka/:brandSlug/:categorySlug',
    loadComponent: () => import('./brand-page/brand-page').then((m) => m.BrandPage),
  },
  {
    path: 'kategoriler',
    loadComponent: () => import('./category-list-page/category-list-page').then((m) => m.CategoryListPage),
  },
  {
    path: 'kategori/:categorySlug',
    loadComponent: () => import('./category-page/category-page').then((m) => m.CategoryPage),
  },
  {
    path: 'gizlilik-politikasi',
    loadComponent: () => import('./privacy-policy-page/privacy-policy-page').then((m) => m.PrivacyPolicyPage),
  },
  {
    path: 'cerez-politikasi',
    loadComponent: () => import('./cookie-policy-page/cookie-policy-page').then((m) => m.CookiePolicyPage),
  },
  {
    path: 'rehber',
    loadComponent: () => import('./article-list-page/article-list-page').then((m) => m.ArticleListPage),
  },
  {
    path: 'rehber/:slug',
    loadComponent: () => import('./article-page/article-page').then((m) => m.ArticlePage),
  },
  {
    path: 'nasil-calisiyoruz',
    loadComponent: () => import('./how-it-works-page/how-it-works-page').then((m) => m.HowItWorksPage),
  },
  {
    path: 'hakkimizda',
    loadComponent: () => import('./about-page/about-page').then((m) => m.AboutPage),
  },
  {
    path: 'iletisim',
    loadComponent: () => import('./contact-page/contact-page').then((m) => m.ContactPage),
  },
  {
    path: 'sozluk',
    loadComponent: () => import('./glossary-page/glossary-page').then((m) => m.GlossaryPage),
  },
  {
    path: 'urun-inceleme/:id/:slug',
    loadComponent: () => import('./product-review-page/product-review-page').then((m) => m.ProductReviewPage),
  },
  {
    path: 'hesaplama',
    loadComponent: () => import('./calculator-list-page/calculator-list-page').then((m) => m.CalculatorListPage),
  },
  // SIRA ÖNEMLİ: spesifik hesaplayıcı route'ları, en alttaki generic
  // 'hesaplama/:slug'dan ÖNCE gelmeli.
  {
    path: 'hesaplama/protein-ihtiyaci',
    loadComponent: () => import('./protein-calculator-page/protein-calculator-page').then((m) => m.ProteinCalculatorPage),
  },
  // Vücut hesaplayıcıları (kalori/TDEE, BMI, su) — route'lar konfigürasyondan
  // üretiliyor, yeni bir araç eklemek için burayı düzenlemeye gerek yok.
  // Slug'ı bileşene ROUTE PARAMETRESİ olarak veriyoruz (':slug'), path'i
  // sabit yazıp `data` ile geçirmek denendi ama bileşene ulaşmadı.
  // 'hesaplama/:slug' generic route'undan önce geldikleri için doğru
  // bileşene düşüyorlar.
  ...BODY_CALCULATORS.map((calc) => ({
    matcher: (segments: UrlSegment[]) =>
      segments.length === 2 && segments[0].path === 'hesaplama' && segments[1].path === calc.slug
        ? { consumed: segments, posParams: { slug: segments[1] } }
        : null,
    loadComponent: () => import('./body-calculator-page/body-calculator-page').then((m) => m.BodyCalculatorPage),
  })),
  // Takviye doz + maliyet hesaplayıcıları (kreatin, beta-alanine, sitrülin,
  // EAA) — hepsi tek bileşen, konfigürasyonla ayrışıyor. Generic olduğu için
  // EN SONDA: eşleşmeyen bir slug burada yakalanıp /hesaplama'ya yönleniyor.
  {
    path: 'hesaplama/:slug',
    loadComponent: () => import('./supplement-dosage-page/supplement-dosage-page').then((m) => m.SupplementDosagePage),
  },
  {
    path: 'favorilerim',
    loadComponent: () => import('./favorites-page/favorites-page').then((m) => m.FavoritesPage),
  },
  {
    path: 'karsilastir/:pair',
    loadComponent: () => import('./brand-comparison-page/brand-comparison-page').then((m) => m.BrandComparisonPage),
  },
  // Ürün karşılaştırma — marka karşılaştırmasından ('karsilastir/:pair')
  // ayrı bir adres, ikisi karışmasın diye.
  {
    path: 'karsilastir-urun/:pair',
    loadComponent: () => import('./product-comparison-page/product-comparison-page').then((m) => m.ProductComparisonPage),
  },
];

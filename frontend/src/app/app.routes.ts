import { Routes, UrlSegment } from '@angular/router';

import { AboutPage } from './about-page/about-page';
import { ArticleListPage } from './article-list-page/article-list-page';
import { GlossaryPage } from './glossary-page/glossary-page';
import { ArticlePage } from './article-page/article-page';
import { BrandPage } from './brand-page/brand-page';
import { CategoryListPage } from './category-list-page/category-list-page';
import { CategoryPage } from './category-page/category-page';
import { BrandComparisonPage } from './brand-comparison-page/brand-comparison-page';
import { CookiePolicyPage } from './cookie-policy-page/cookie-policy-page';
import { DealsList } from './deals-list/deals-list';
import { FavoritesPage } from './favorites-page/favorites-page';
import { HowItWorksPage } from './how-it-works-page/how-it-works-page';
import { PrivacyPolicyPage } from './privacy-policy-page/privacy-policy-page';
import { ProductComparisonPage } from './product-comparison-page/product-comparison-page';
import { BodyCalculatorPage } from './body-calculator-page/body-calculator-page';
import { CalculatorListPage } from './calculator-list-page/calculator-list-page';
import { BODY_CALCULATORS } from './core/body-calculators';
import { ProteinCalculatorPage } from './protein-calculator-page/protein-calculator-page';
import { SupplementDosagePage } from './supplement-dosage-page/supplement-dosage-page';

export const routes: Routes = [
  { path: '', component: DealsList },
  { path: 'urun/:id', component: DealsList },
  { path: 'urun/:id/:slug', component: DealsList },
  { path: 'marka/:brandSlug/indirim-kodu', component: BrandPage },
  // Marka × kategori kesişimi ("hardline protein tozu fiyatları" gibi
  // aramalar için). SIRA ÖNEMLİ: 'indirim-kodu' bu satırdan ÖNCE tanımlı
  // olmalı, yoksa o da bir kategori slug'ı sanılır.
  { path: 'marka/:brandSlug/:categorySlug', component: BrandPage },
  { path: 'kategoriler', component: CategoryListPage },
  { path: 'kategori/:categorySlug', component: CategoryPage },
  { path: 'gizlilik-politikasi', component: PrivacyPolicyPage },
  { path: 'cerez-politikasi', component: CookiePolicyPage },
  { path: 'rehber', component: ArticleListPage },
  { path: 'rehber/:slug', component: ArticlePage },
  { path: 'nasil-calisiyoruz', component: HowItWorksPage },
  { path: 'hakkimizda', component: AboutPage },
  { path: 'sozluk', component: GlossaryPage },
  { path: 'hesaplama', component: CalculatorListPage },
  // SIRA ÖNEMLİ: spesifik hesaplayıcı route'ları, en alttaki generic
  // 'hesaplama/:slug'dan ÖNCE gelmeli.
  { path: 'hesaplama/protein-ihtiyaci', component: ProteinCalculatorPage },
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
    component: BodyCalculatorPage,
  })),
  // Takviye doz + maliyet hesaplayıcıları (kreatin, beta-alanine, sitrülin,
  // EAA) — hepsi tek bileşen, konfigürasyonla ayrışıyor. Generic olduğu için
  // EN SONDA: eşleşmeyen bir slug burada yakalanıp /hesaplama'ya yönleniyor.
  { path: 'hesaplama/:slug', component: SupplementDosagePage },
  { path: 'favorilerim', component: FavoritesPage },
  { path: 'karsilastir/:pair', component: BrandComparisonPage },
  // Ürün karşılaştırma — marka karşılaştırmasından ('karsilastir/:pair')
  // ayrı bir adres, ikisi karışmasın diye.
  { path: 'karsilastir-urun/:pair', component: ProductComparisonPage },
];

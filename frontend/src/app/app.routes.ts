import { Routes } from '@angular/router';

import { ArticleListPage } from './article-list-page/article-list-page';
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
import { ProteinCalculatorPage } from './protein-calculator-page/protein-calculator-page';

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
  { path: 'hesaplama/protein-ihtiyaci', component: ProteinCalculatorPage },
  { path: 'favorilerim', component: FavoritesPage },
  { path: 'karsilastir/:pair', component: BrandComparisonPage },
];

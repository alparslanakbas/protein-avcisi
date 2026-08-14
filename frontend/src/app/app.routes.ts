import { Routes } from '@angular/router';

import { ArticleListPage } from './article-list-page/article-list-page';
import { ArticlePage } from './article-page/article-page';
import { BrandPage } from './brand-page/brand-page';
import { CategoryPage } from './category-page/category-page';
import { CookiePolicyPage } from './cookie-policy-page/cookie-policy-page';
import { DealsList } from './deals-list/deals-list';
import { HowItWorksPage } from './how-it-works-page/how-it-works-page';
import { PrivacyPolicyPage } from './privacy-policy-page/privacy-policy-page';

export const routes: Routes = [
  { path: '', component: DealsList },
  { path: 'urun/:id', component: DealsList },
  { path: 'marka/:brandSlug/indirim-kodu', component: BrandPage },
  { path: 'kategori/:categorySlug', component: CategoryPage },
  { path: 'gizlilik-politikasi', component: PrivacyPolicyPage },
  { path: 'cerez-politikasi', component: CookiePolicyPage },
  { path: 'rehber', component: ArticleListPage },
  { path: 'rehber/:slug', component: ArticlePage },
  { path: 'nasil-calisiyoruz', component: HowItWorksPage },
];

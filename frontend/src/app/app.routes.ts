import { Routes } from '@angular/router';

import { BrandPage } from './brand-page/brand-page';
import { CategoryPage } from './category-page/category-page';
import { DealsList } from './deals-list/deals-list';

export const routes: Routes = [
  { path: '', component: DealsList },
  { path: 'urun/:id', component: DealsList },
  { path: 'marka/:brandSlug/indirim-kodu', component: BrandPage },
  { path: 'kategori/:categorySlug', component: CategoryPage },
];

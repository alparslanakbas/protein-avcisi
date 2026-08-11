import { Routes } from '@angular/router';

import { DealsList } from './deals-list/deals-list';

export const routes: Routes = [
  { path: '', component: DealsList },
  { path: 'urun/:id', component: DealsList },
];

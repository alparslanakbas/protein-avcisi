import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { PriceHistory } from './price-history.model';

@Injectable({ providedIn: 'root' })
export class PriceHistoryService {
  private readonly http = inject(HttpClient);

  get(productId: number, days: number): Observable<PriceHistory> {
    return this.http.get<PriceHistory>(`${API_BASE_URL}/api/products/${productId}/price-history`, {
      params: { days },
    });
  }

  goToStoreUrl(productId: number): string {
    return `${API_BASE_URL}/go/${productId}`;
  }
}

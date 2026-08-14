import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';

@Injectable({ providedIn: 'root' })
export class ProductFeedbackService {
  private readonly http = inject(HttpClient);

  vote(productId: number, helpful: boolean): Observable<void> {
    return this.http.post<void>(`${API_BASE_URL}/api/products/${productId}/vote`, { helpful });
  }
}

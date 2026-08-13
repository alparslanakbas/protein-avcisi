import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';

@Injectable({ providedIn: 'root' })
export class WatchService {
  private readonly http = inject(HttpClient);

  watch(productId: number, email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API_BASE_URL}/api/products/${productId}/watch`, { email });
  }
}

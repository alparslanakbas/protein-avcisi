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

  // Bilinçli olarak API_BASE_URL değil, göreceli bir yol — böylece link
  // ziyaretçinin gördüğü sitenin kendi domain'inde kalıyor (bilinmeyen bir
  // "api.protein-avcisi..." adresine gitmek, dikkatli kullanıcılara
  // phishing linki gibi görünüyordu). server.ts'te bu yol backend'in
  // gerçek /go/{id}'sine sunucu tarafında proxy'leniyor.
  goToStoreUrl(productId: number): string {
    return `/go/${productId}`;
  }
}

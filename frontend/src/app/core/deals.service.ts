import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Deal } from './deal.model';

@Injectable({ providedIn: 'root' })
export class DealsService {
  constructor(private readonly http: HttpClient) {}

  getDeals(brand?: string): Observable<Deal[]> {
    return this.http.get<Deal[]>(`${API_BASE_URL}/api/deals`, { params: this.buildParams(brand) });
  }

  getAllProducts(brand?: string): Observable<Deal[]> {
    return this.http.get<Deal[]>(`${API_BASE_URL}/api/products`, { params: this.buildParams(brand) });
  }

  private buildParams(brand?: string): HttpParams {
    let params = new HttpParams();
    if (brand) {
      params = params.set('brand', brand);
    }
    return params;
  }
}

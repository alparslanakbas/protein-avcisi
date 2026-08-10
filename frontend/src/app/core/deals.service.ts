import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { Deal } from './deal.model';

const API_BASE_URL = 'http://localhost:5156';

@Injectable({ providedIn: 'root' })
export class DealsService {
  constructor(private readonly http: HttpClient) {}

  getDeals(brand?: string): Observable<Deal[]> {
    const url = brand
      ? `${API_BASE_URL}/api/deals?brand=${encodeURIComponent(brand)}`
      : `${API_BASE_URL}/api/deals`;
    return this.http.get<Deal[]>(url);
  }
}

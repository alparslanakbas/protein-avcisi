import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { BrandComparison } from './brand-comparison.model';

@Injectable({ providedIn: 'root' })
export class BrandComparisonService {
  private readonly http = inject(HttpClient);

  compare(brand1: string, brand2: string): Observable<BrandComparison> {
    return this.http.get<BrandComparison>(`${API_BASE_URL}/api/brand-comparison`, {
      params: { brand1, brand2 },
    });
  }
}

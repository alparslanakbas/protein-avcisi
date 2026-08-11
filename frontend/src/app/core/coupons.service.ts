import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Coupon } from './coupon.model';

@Injectable({ providedIn: 'root' })
export class CouponsService {
  constructor(private readonly http: HttpClient) {}

  getCoupons(): Observable<Coupon[]> {
    return this.http.get<Coupon[]>(`${API_BASE_URL}/api/coupons`);
  }
}

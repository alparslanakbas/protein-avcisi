import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Deal } from './deal.model';
import { FilterOptions, PagedResult } from './paged-result.model';

export interface DealsQuery {
  brands?: string[];
  categories?: string[];
  search?: string;
  minPrice?: number | null;
  maxPrice?: number | null;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class DealsService {
  constructor(private readonly http: HttpClient) {}

  getDeals(query: DealsQuery): Observable<PagedResult<Deal>> {
    return this.http.get<PagedResult<Deal>>(`${API_BASE_URL}/api/deals`, { params: this.buildParams(query) });
  }

  getAllProducts(query: DealsQuery): Observable<PagedResult<Deal>> {
    return this.http.get<PagedResult<Deal>>(`${API_BASE_URL}/api/products`, { params: this.buildParams(query) });
  }

  getStoreDeals(query: DealsQuery): Observable<PagedResult<Deal>> {
    return this.http.get<PagedResult<Deal>>(`${API_BASE_URL}/api/store-deals`, { params: this.buildParams(query) });
  }

  getFilterOptions(): Observable<FilterOptions> {
    return this.http.get<FilterOptions>(`${API_BASE_URL}/api/filters`);
  }

  private buildParams(query: DealsQuery): HttpParams {
    let params = new HttpParams();

    for (const brand of query.brands ?? []) {
      params = params.append('brands', brand);
    }
    for (const category of query.categories ?? []) {
      params = params.append('categories', category);
    }
    if (query.search) params = params.set('search', query.search);
    if (query.minPrice != null) params = params.set('minPrice', query.minPrice);
    if (query.maxPrice != null) params = params.set('maxPrice', query.maxPrice);
    if (query.page) params = params.set('page', query.page);
    if (query.pageSize) params = params.set('pageSize', query.pageSize);

    return params;
  }
}

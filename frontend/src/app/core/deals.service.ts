import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, shareReplay, throwError } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Deal } from './deal.model';
import { FilterOptions, PagedResult } from './paged-result.model';

export interface DealsQuery {
  brands?: string[];
  categories?: string[];
  search?: string;
  minPrice?: number | null;
  maxPrice?: number | null;
  sortBy?: string;
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

  getProductById(id: number): Observable<Deal> {
    return this.http.get<Deal>(`${API_BASE_URL}/api/products/${id}`);
  }

  // Header, ana sayfa, marka ve kategori sayfaları hepsi kendi başlangıcında
  // bunu ayrı ayrı çağırıyordu (aynı sayfa yüklemesinde 4 ayrı /api/filters
  // isteği) — marka/kategori listesi neredeyse hiç değişmediği için tek
  // istek paylaşılıp önbelleğe alınıyor. Hata durumunda önbellek sıfırlanıp
  // sonraki çağrı gerçekten yeniden dener (shareReplay hatayı da sonsuza
  // kadar tekrar oynatır, bu istemiyoruz).
  private filterOptions$: Observable<FilterOptions> | null = null;

  getFilterOptions(): Observable<FilterOptions> {
    if (!this.filterOptions$) {
      this.filterOptions$ = this.http.get<FilterOptions>(`${API_BASE_URL}/api/filters`).pipe(
        catchError((err) => {
          this.filterOptions$ = null;
          return throwError(() => err);
        }),
        shareReplay(1),
      );
    }
    return this.filterOptions$;
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
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.page) params = params.set('page', query.page);
    if (query.pageSize) params = params.set('pageSize', query.pageSize);

    return params;
  }
}

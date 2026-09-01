import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, of, shareReplay, throwError } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { BrandStats } from './brand-stats.model';
import { CategoryPriceStats } from './category-price-stats.model';
import { Deal } from './deal.model';
import { HomepageStats } from './homepage-stats.model';
import { FilterOptions, PagedResult } from './paged-result.model';
import { ProductSparkline } from './product-sparkline.model';

// Marka × kategori kesişimi — hangi markanın hangi kategoride kaç ürünü var.
export interface BrandCategoryPair {
  brandName: string;
  category: string;
  productCount: number;
}

export interface DealsQuery {
  brands?: string[];
  sellers?: string[];
  categories?: string[];
  search?: string;
  minPrice?: number | null;
  maxPrice?: number | null;
  sortBy?: string;
  page?: number;
  pageSize?: number;
  // Belirli bir bileşeni arayan sayfalar için false gönderilir — eşanlamlı
  // genişletme orada kategorinin tamamını getiriyor (bkz. backend'deki
  // expandSearchSynonyms açıklaması).
  expandSynonyms?: boolean;
  // Marka SAYFASI true gönderiyor: markanın kendi sitesinden ürünü varsa
  // yalnızca onlar listelenir, bayideki kopyası aynı sayfada yan yana durup
  // ürünü iki kez göstermez. Markanın hiç doğrudan ürünü yoksa süzgeç
  // uygulanmaz — yoksa yalnızca bayiden gelen markaların sayfaları boşalırdı.
  // Ana sayfadaki marka filtresi bunu KULLANMIYOR; orada marka ve satıcı
  // filtreleri birbirinden bağımsız kalmalı.
  preferBrandStore?: boolean;
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

  // Ana sayfadaki "canlı tarama şeridi" için — her sayfa yüklemesinde bir kez.
  getStats(): Observable<HomepageStats> {
    return this.http.get<HomepageStats>(`${API_BASE_URL}/api/stats`);
  }

  // Ana sayfadaki "Kullanıcıların tercih ettikleri" bandı. Sıralama
  // sunucuda gerçek favori ve tıklama sayaçlarından hesaplanıyor; istemci
  // bu sırayı olduğu gibi koruyor.
  getPreferredProducts(count = 60): Observable<Deal[]> {
    return this.http.get<Deal[]>(`${API_BASE_URL}/api/preferred-products`, {
      params: new HttpParams().set('count', count),
    });
  }

  // Marka sayfasındaki "bu markaya genel bakış" bölümü için — kendi
  // verimize dayanan özgün istatistik, markanın kopyalanmış tarihçesi
  // yerine (bkz. CLAUDE.md "marka sayfaları" tartışması).
  // category verilirse istatistikler markanın yalnızca o kategorideki
  // ürünlerinden hesaplanır — marka × kategori sayfalarının kendi verisi.
  getBrandStats(brand: string, category?: string): Observable<BrandStats> {
    let params = new HttpParams().set('brand', brand);
    if (category) params = params.set('category', category);
    return this.http.get<BrandStats>(`${API_BASE_URL}/api/brand-stats`, { params });
  }

  // Ürün incelemesi sayfasındaki "kategorisinde nasıl konumlanıyor" bölümü
  // için. Kategoride hiç aktif ürün yoksa backend 404 dönüyor — component
  // bu durumda bölümü hiç göstermemeli, hata olarak ele almıyoruz.
  getCategoryPriceStats(category: string): Observable<CategoryPriceStats | null> {
    const params = new HttpParams().set('category', category);
    return this.http
      .get<CategoryPriceStats>(`${API_BASE_URL}/api/category-price-stats`, { params })
      .pipe(catchError(() => of(null)));
  }

  // Protein hesaplayıcısının "servis başı en uygun ürünler" tablosu için.
  // Hesap backend'de yapılıyor ve yalnızca ilk N ürün dönüyor — sayfanın
  // tüm kategoriyi (100 ürün) çekmesi SSR çıktısını 451 KB'a çıkarıyordu.
  getBestValuePerServing(query: {
    category: string;
    brands?: string[];
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<Deal>> {
    let params = new HttpParams().set('category', query.category);
    for (const brand of query.brands ?? []) params = params.append('brands', brand);
    if (query.search) params = params.set('search', query.search);
    if (query.page) params = params.set('page', query.page);
    if (query.pageSize) params = params.set('pageSize', query.pageSize);
    return this.http.get<PagedResult<Deal>>(`${API_BASE_URL}/api/best-value-per-serving`, { params });
  }

  // Hesaplayıcı tablosunun marka çipleri — genel /api/filters listesi
  // yanıltıcı olurdu: bir markanın o kategoride ürünü olsa bile porsiyon
  // verisi yoksa çipe tıklandığında tablo boş gelirdi.
  getBestValueBrands(category: string): Observable<string[]> {
    const params = new HttpParams().set('category', category);
    return this.http.get<string[]>(`${API_BASE_URL}/api/best-value-brands`, { params });
  }

  // Marka × kategori kesişim sayfaları — yalnızca gerçekten ürünü olan
  // çiftler. Hem sitemap hem sayfa içi linkler bunu kullanıyor, boş bir
  // kombinasyona sayfa/link üretilmiyor.
  getBrandCategoryPairs(): Observable<BrandCategoryPair[]> {
    return this.http.get<BrandCategoryPair[]>(`${API_BASE_URL}/api/brand-category-pairs`);
  }

  // Ürün kartlarındaki mini sparkline'lar için toplu istek — bir sayfa
  // (24 kart) için tek çağrı, kart başına ayrı istek (N+1) yerine.
  getSparklines(ids: number[], days = 30): Observable<ProductSparkline[]> {
    if (ids.length === 0) return of([]);
    let params = new HttpParams().set('days', days);
    for (const id of ids) params = params.append('ids', id);
    return this.http.get<ProductSparkline[]>(`${API_BASE_URL}/api/products/sparklines`, { params });
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
    for (const seller of query.sellers ?? []) {
      params = params.append('sellers', seller);
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
    // Yalnızca açıkça false verilince gönderiliyor — backend varsayılanı true.
    if (query.expandSynonyms === false) params = params.set('expandSynonyms', 'false');
    // Yalnızca açıkça istenince gönderiliyor — backend varsayılanı false.
    if (query.preferBrandStore) params = params.set('preferBrandStore', 'true');

    return params;
  }
}

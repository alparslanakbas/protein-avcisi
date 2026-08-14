import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { Observable, of } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Deal } from './deal.model';

const TOKEN_KEY = 'favorites-token';

// Hesap/login gerektirmeyen "favorilerim" listesi — ilk eklemede backend'in
// döndürdüğü token localStorage'da tutulup sonraki isteklerde kullanılıyor
// (ThemeService/CookieConsentService ile aynı SSR-güvenli desende).
@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private readonly http = inject(HttpClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  getToken(): string | null {
    return this.isBrowser ? localStorage.getItem(TOKEN_KEY) : null;
  }

  saveToken(token: string): void {
    if (this.isBrowser) localStorage.setItem(TOKEN_KEY, token);
  }

  add(productId: number, email?: string): Observable<{ token: string }> {
    return this.http.post<{ token: string }>(`${API_BASE_URL}/api/products/${productId}/favorite`, {
      token: this.getToken(),
      email: email ?? null,
    });
  }

  remove(productId: number): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/api/products/${productId}/favorite`, {
      params: { token: this.getToken() ?? '' },
    });
  }

  list(): Observable<Deal[]> {
    const token = this.getToken();
    if (!token) return of([]);
    return this.http.get<Deal[]>(`${API_BASE_URL}/api/favorites`, { params: { token } });
  }
}

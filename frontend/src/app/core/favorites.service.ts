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

  saveToken(token: string | null): void {
    // token null gelebilir — e-posta zaten başka bir aboneye aitse backend
    // artık o hesabın token'ını ifşa etmiyor (bkz. 2026-08-15 güvenlik
    // düzeltmesi), bu durumda localStorage'a hiçbir şey yazmıyoruz.
    if (this.isBrowser && token) localStorage.setItem(TOKEN_KEY, token);
  }

  add(productId: number, email?: string): Observable<{ token: string | null }> {
    return this.http.post<{ token: string | null }>(`${API_BASE_URL}/api/products/${productId}/favorite`, {
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

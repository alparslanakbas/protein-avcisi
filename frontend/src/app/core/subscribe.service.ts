import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';

@Injectable({ providedIn: 'root' })
export class SubscribeService {
  private readonly http = inject(HttpClient);

  /**
   * `website` bir bal küpü: form içinde gizli duruyor, gerçek kullanıcı hiç
   * görmüyor ve boş gönderiyor. Otomatik doldurma yapan botlar dolduruyor;
   * sunucu dolu gelen isteği sessizce yok sayıyor.
   */
  subscribe(email: string, website = ''): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API_BASE_URL}/api/subscribe`, { email, website });
  }
}

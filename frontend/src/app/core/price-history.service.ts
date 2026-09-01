import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { PriceHistory } from './price-history.model';

@Injectable({ providedIn: 'root' })
export class PriceHistoryService {
  private readonly http = inject(HttpClient);

  get(productId: number, days: number): Observable<PriceHistory> {
    return this.http.get<PriceHistory>(`${API_BASE_URL}/api/products/${productId}/price-history`, {
      params: { days },
    });
  }

  /**
   * "Mağazaya git" bağlantısının adresi.
   *
   * Ürünün ortaklık kodu eklenmiş mağaza adresi elimizdeyse DOĞRUDAN oraya
   * gidiyoruz. Eskiden her zaman kendi sitemizdeki /go/{id} ucuna gidilir,
   * o da 302 ile mağazaya atardı; kurulu PWA'da araya giren bu yönlendirme
   * geri tuşunu ÖLDÜRÜYORDU (yeni tarama bağlamının geçmişinde yalnızca
   * yönlendirme zinciri kalıyor, geri basınca bağlam kapanıp kullanıcı
   * uygulamadan çıkıyordu — kullanıcı bildirdi, ölçümle doğrulandı).
   *
   * Adres yoksa (eski önbellekten gelen yanıt) /go/{id} yedeği kalıyor.
   */
  goToStoreUrl(productId: number, storeUrl?: string | null): string {
    return storeUrl ?? `/go/${productId}`;
  }

  /**
   * Mağaza tıklamasını sayar.
   *
   * Bağlantı artık doğrudan mağazaya gittiği için sayacı /go/{id} artıramıyor.
   * sendBeacon kullanılıyor: sayfa mağazaya giderken bile isteğin gönderilmesi
   * garanti, gövde boş olduğu için istek "basit" kalıyor ve CORS ön kontrolü
   * tetiklenmiyor (ön kontrol, sayfa ayrılırken iptal edilip sayacı
   * kaybettirebilirdi).
   */
  trackStoreClick(productId: number): void {
    if (typeof navigator === 'undefined' || !navigator.sendBeacon) return;
    try {
      navigator.sendBeacon(`${API_BASE_URL}/api/products/${productId}/click`);
    } catch {
      // Sayaç kaybı, mağazaya gidişi engellemeyi haklı çıkarmaz.
    }
  }
}

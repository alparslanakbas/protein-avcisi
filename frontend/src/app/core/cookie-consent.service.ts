import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';

export type CookieConsentStatus = 'accepted' | 'rejected' | null;

const STORAGE_KEY = 'cookie-consent';

// Şu an sitede reklam/analitik çerezi yok, bu yüzden bu tercihin henüz
// gerçekte hiçbir şeyi engellemesi/izin vermesi gerekmiyor — AdSense/
// Analytics eklenmeden ÖNCE altyapı hazır olsun diye kuruldu (bkz.
// CLAUDE.md). İleride bir script eklenirse yükleme kararı
// `status() === 'accepted'` kontrolüne bağlanacak, banner'a hiç
// dokunulmayacak.
@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly status = signal<CookieConsentStatus>(this.readStoredStatus());

  accept(): void {
    this.setStatus('accepted');
  }

  reject(): void {
    this.setStatus('rejected');
  }

  private setStatus(status: 'accepted' | 'rejected'): void {
    this.status.set(status);
    if (this.isBrowser) {
      localStorage.setItem(STORAGE_KEY, status);
    }
  }

  private readStoredStatus(): CookieConsentStatus {
    if (!this.isBrowser) return null;
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'accepted' || stored === 'rejected' ? stored : null;
  }
}

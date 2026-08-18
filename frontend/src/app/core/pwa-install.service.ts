import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';

const DISMISS_KEY = 'pwa-install-dismissed';

// Standart DOM tipleri BeforeInstallPromptEvent'i tanımıyor (henüz tüm
// tarayıcılarda desteklenmiyor, sadece Chromium tabanlılarda) — kendimiz
// dar bir arayüz tanımlıyoruz.
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

// "Ana ekrana ekle" ipucu için — tarayıcının kendi otomatik mini-infobar'ı
// yerine site tasarımına uyan bir şerit gösterebilelim diye `beforeinstallprompt`
// event'ini yakalayıp saklıyoruz. Bu event SADECE Chromium tabanlı tarayıcılarda
// (Android Chrome dahil) ve site zaten PWA kriterlerini (manifest + service
// worker) sağladığında ateşleniyor — iOS Safari hiç ateşlemiyor, bu yüzden
// ekstra bir platform kontrolüne gerek kalmadan doğal olarak sadece
// desteklenen ortamlarda `canInstall` true oluyor.
@Injectable({ providedIn: 'root' })
export class PwaInstallService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private deferredPrompt: BeforeInstallPromptEvent | null = null;

  readonly canInstall = signal(false);

  constructor() {
    if (!this.isBrowser || localStorage.getItem(DISMISS_KEY)) return;

    window.addEventListener('beforeinstallprompt', (event) => {
      event.preventDefault();
      this.deferredPrompt = event as BeforeInstallPromptEvent;
      this.canInstall.set(true);
    });

    window.addEventListener('appinstalled', () => {
      this.deferredPrompt = null;
      this.canInstall.set(false);
    });
  }

  async promptInstall(): Promise<void> {
    if (!this.deferredPrompt) return;
    await this.deferredPrompt.prompt();
    await this.deferredPrompt.userChoice;
    this.deferredPrompt = null;
    this.canInstall.set(false);
  }

  dismiss(): void {
    this.canInstall.set(false);
    if (this.isBrowser) localStorage.setItem(DISMISS_KEY, '1');
  }
}

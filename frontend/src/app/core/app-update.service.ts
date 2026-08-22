import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';

// Yeni bir deploy yayına girdiğinde, o anda sitede açık olan sekmeler
// bunu kendiliğinden fark etmiyor — service worker arka planda yeni
// versiyonu indirse bile kullanıcı elle "yenile" yapmadan eski JS ile
// gezinmeye devam ediyordu (CLAUDE.md'de not edilen bir eksiklikti).
// Bu servis `VERSION_READY` event'ini dinleyip basit bir signal'e
// çeviriyor, banner bu signal'e bakıp "yenile" butonu gösteriyor.
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly swUpdate = inject(SwUpdate);

  readonly updateAvailable = signal(false);

  constructor() {
    if (!this.isBrowser || !this.swUpdate.isEnabled) return;

    this.swUpdate.versionUpdates
      .pipe(filter((event): event is VersionReadyEvent => event.type === 'VERSION_READY'))
      .subscribe(() => this.updateAvailable.set(true));

    // registerWhenStable:30000 sadece İLK kaydı geciktiriyor, service
    // worker'ın kendisi periyodik olarak yeni versiyon aramıyor — bu
    // yüzden sekme uzun süre açık kalırsa hiç kontrol edilmez. Saatte
    // bir elle kontrol tetikliyoruz (ağır bir işlem değil, sadece
    // ngsw.json'un ETag'ini kontrol ediyor).
    setInterval(() => this.swUpdate.checkForUpdate(), 60 * 60 * 1000);
  }

  // Sadece location.reload() çağırmak yeterli DEĞİL — yeni service worker
  // "waiting" durumunda kalmaya devam eder, sayfa hâlâ ESKİ (etkin) SW
  // tarafından kontrol edilir. activateUpdate() yeni SW'ye "hemen etkinleş"
  // sinyali gönderiyor (SKIP_WAITING), reload ondan SONRA yapılmalı —
  // aksi halde kullanıcı "yenile"ye bassa bile hiçbir şey değişmiyordu
  // (gerçek prod bug'ı, kullanıcı bildirdi: mağazaya git düzeltmesi deploy
  // sonrası bile etkisiz kalıyordu).
  reload(): void {
    if (!this.isBrowser) return;
    this.swUpdate
      .activateUpdate()
      .catch(() => undefined)
      .finally(() => window.location.reload());
  }
}

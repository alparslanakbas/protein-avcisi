import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners, isDevMode } from '@angular/core';
import { PreloadAllModules, RouteReuseStrategy, provideRouter, withPreloading } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideClientHydration, withIncrementalHydration } from '@angular/platform-browser';

import { routes } from './app.routes';
import { DealsRouteReuseStrategy } from './core/deals-route-reuse.strategy';
import { provideServiceWorker } from '@angular/service-worker';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Lazy route'lar (app.routes.ts) ilk yükte indirilmiyor ama
    // PreloadAllModules ile ana sayfa yüklenip tarayıcı boşa düşünce
    // (idle) arka planda hepsi önceden çekiliyor — kullanıcı bir linke
    // tıkladığında ekstra ağ gecikmesi yaşanmıyor, sadece ilk yük küçülüyor.
    provideRouter(routes, withPreloading(PreloadAllModules)),
    provideHttpClient(withFetch()),
    // Artımlı hydration: @defer (hydrate on ...) ile işaretlenen bloklar
    // sunucuda yine render ediliyor (arama motorları HTML'de görüyor) ama
    // tarayıcıda tetikleyici gelene kadar canlandırılmıyor. Ana sayfa ilk
    // açılışta 320 ms'lik bir engelleme süresi üretiyordu; bunun kaynağı
    // veri değil, ekranda görünmeyen blokların da baştan canlandırılmasıydı.
    // Olay tekrarı da bununla birlikte geliyor: henüz canlanmamış bir bloğa
    // yapılan tıklama kaybolmuyor, hydration bitince uygulanıyor.
    provideClientHydration(withIncrementalHydration()),
    { provide: LOCALE_ID, useValue: 'tr-TR' },
    { provide: RouteReuseStrategy, useClass: DealsRouteReuseStrategy },
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};

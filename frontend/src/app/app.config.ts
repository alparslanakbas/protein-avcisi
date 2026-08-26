import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners, isDevMode } from '@angular/core';
import { PreloadAllModules, RouteReuseStrategy, provideRouter, withPreloading } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideClientHydration } from '@angular/platform-browser';

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
    provideClientHydration(),
    { provide: LOCALE_ID, useValue: 'tr-TR' },
    { provide: RouteReuseStrategy, useClass: DealsRouteReuseStrategy },
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};

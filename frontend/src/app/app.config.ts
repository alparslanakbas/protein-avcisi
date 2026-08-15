import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { RouteReuseStrategy, provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptorsFromDi } from '@angular/common/http';
import { provideClientHydration } from '@angular/platform-browser';

import { routes } from './app.routes';
import { DealsRouteReuseStrategy } from './core/deals-route-reuse.strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // withInterceptorsFromDi() olmadan klasik HTTP_INTERCEPTORS token'lı
    // (class tabanlı) interceptor'lar sessizce hiç çalışmıyor — SSR'a özel
    // SsrInternalHeaderInterceptor (app.config.server.ts) bu yüzden gerekli.
    // Tarayıcı tarafında HTTP_INTERCEPTORS ile kayıtlı başka bir şey
    // olmadığı için burada zararsız bir no-op.
    provideHttpClient(withFetch(), withInterceptorsFromDi()),
    provideClientHydration(),
    { provide: RouteReuseStrategy, useClass: DealsRouteReuseStrategy },
  ],
};

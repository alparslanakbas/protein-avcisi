import { mergeApplicationConfig, ApplicationConfig, Provider } from '@angular/core';
import { HTTP_TRANSFER_CACHE_ORIGIN_MAP } from '@angular/common/http';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_BASE_URL } from './core/api.config';
import { INTERNAL_API_BASE_URL } from './core/internal-api';

// Docker'da http://backend:8080 (docker-compose.yml). Yerelde tanımsız,
// o zaman SSR eskisi gibi API_BASE_URL'e gider.
const internalApiBase = process.env['API_INTERNAL_URL']?.replace(/\/+$/, '') || null;

// Aktarım önbelleği (TransferState) anahtarı istek adresinden üretiliyor.
// Sunucu iç adrese gidip tarayıcı genel adresi sorunca anahtarlar tutmaz ve
// tarayıcı sunucunun zaten çektiği veriyi hidrasyonda YENİDEN indirirdi.
// Bu eşleme sunucuda kaydederken iç adresi genel adrese çeviriyor.
const internalApiProviders: Provider[] = internalApiBase
  ? [
      { provide: INTERNAL_API_BASE_URL, useValue: internalApiBase },
      { provide: HTTP_TRANSFER_CACHE_ORIGIN_MAP, useValue: { [internalApiBase]: API_BASE_URL } },
    ]
  : [];

const serverConfig: ApplicationConfig = {
  providers: [provideServerRendering(withRoutes(serverRoutes)), ...internalApiProviders],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);

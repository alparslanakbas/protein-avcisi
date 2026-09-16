import { HttpInterceptorFn } from '@angular/common/http';
import { InjectionToken, inject } from '@angular/core';

import { API_BASE_URL } from './api.config';

// SSR'IN API'YE İÇ AĞDAN GİTMESİ (16 Eylül).
//
// Sunucuda render edilen her sayfa API'yi genel adresten
// (https://api.proteinavcisi.com.tr) çağırıyordu: istek VM'den çıkıp
// Cloudflare'e gidiyor, oradan aynı VM'e geri dönüyordu. Caddy logunda
// isteklerin %81-85'i bu iç çağrılardı (User-Agent "node", kaynak VM'in
// kendi adresi). İki zararı var: her sayfa gereksiz bir TLS + Cloudflare
// turu ödüyor, ve bir saldırı anında ziyaretçi başına düşen ~5 iç çağrı
// Cloudflare'in hız sınırlarına ve kotasına bizim kendi trafiğimiz olarak
// yazılıyor. Docker ağında backend doğrudan http://backend:8080'de.
//
// Adres yalnızca SUNUCUDA değiştiriliyor (token yalnızca app.config.server.ts
// ve server.ts'te veriliyor); tarayıcı eskisi gibi genel adrese gidiyor.
// Ortam değişkeni tanımsızsa (yerel geliştirme) hiçbir şey değişmiyor.
export const INTERNAL_API_BASE_URL = new InjectionToken<string | null>('INTERNAL_API_BASE_URL', {
  factory: () => null,
});

// Backend'in çıktı önbelleği anahtarında şema var; iç istek http olduğu için
// bu başlık olmadan ısıtılmış (https) girdiler hiç kullanılmazdı. Host
// anahtardan çıkarıldı (Node'un fetch'i Host başlığını değiştirmiyor, ölçüldü).
export const INTERNAL_API_HEADERS: Readonly<Record<string, string>> = { 'X-Forwarded-Proto': 'https' };

export function toInternalApiUrl(url: string, internalBase: string | null | undefined): string {
  if (!internalBase) return url;
  if (url === API_BASE_URL || url.startsWith(`${API_BASE_URL}/`)) {
    return internalBase.replace(/\/+$/, '') + url.slice(API_BASE_URL.length);
  }
  return url;
}

export const internalApiInterceptor: HttpInterceptorFn = (req, next) => {
  const internalBase = inject(INTERNAL_API_BASE_URL);
  const url = toInternalApiUrl(req.url, internalBase);
  if (url === req.url) return next(req);
  return next(req.clone({ url, setHeaders: INTERNAL_API_HEADERS }));
};

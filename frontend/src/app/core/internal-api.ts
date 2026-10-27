import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { InjectionToken, inject } from '@angular/core';
import { MonoTypeOperatorFunction, retry, throwError, timer } from 'rxjs';

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

// DEPLOY ANINDA BAĞLANTI HATASINDA YENİDEN DENEME (27 Ekim).
//
// Deploy backend'i yeniden kuruyor; o birkaç saniye backend:8080 bağlantı
// kabul etmiyor ve SSR sayfayı bilerek 503 veriyordu. Caddy'nin beklemesi
// (lb_try_duration) bu yolu kapsamıyor, çünkü SSR backend'e Caddy'siz gidiyor:
// 27 Ekim deploy'unda ±60 sn'de 91 istekten 1'i 38 ms'de 503 aldı.
//
// Yalnız BAĞLANTI hatası (status 0, yanıt hiç gelmedi) ve yalnız GET tekrar
// deneniyor; backend'in verdiği 4xx/5xx gerçek bir yanıt, tekrarlanmıyor.
// Üst sınır 15 sn: Docker eski konteyneri en geç 10 sn'de durduruyor, backend
// 2,3 sn'de dinlemeye başlıyor (ölçüldü). Backend gerçekten düşükse sayfa 15 sn
// geç ama yine 503 alıyor; arıza gizlenmiyor. server.ts'teki apiFetch de aynı
// sınırı kullanıyor.
export const BAGLANTI_YENIDEN_DENEME = { adet: 30, araMs: 500 } as const;

export function baglantiHatasindaYenidenDene<T>(
  adet: number = BAGLANTI_YENIDEN_DENEME.adet,
  araMs: number = BAGLANTI_YENIDEN_DENEME.araMs,
): MonoTypeOperatorFunction<T> {
  return retry({
    count: adet,
    delay: (hata) => (hata instanceof HttpErrorResponse && hata.status === 0 ? timer(araMs) : throwError(() => hata)),
  });
}

// server.ts'teki apiFetch (Node fetch) için daha dar ölçüt: yalnız bağlantının
// HİÇ kurulamadığı hatalar, yani istek backend'e ulaşmamış. /go tıklama
// sayacını artırıyor; yarıda kopan bir istek tekrarlanırsa tıklama iki kez
// sayılırdı. Konteyner silinmişken Docker DNS'i adı çözemiyor (ENOTFOUND).
const BAGLANTI_KURULAMADI = new Set(['ECONNREFUSED', 'ENOTFOUND', 'EAI_AGAIN']);

export function baglantiKurulamadi(hata: unknown): boolean {
  return BAGLANTI_KURULAMADI.has((hata as { cause?: { code?: string } } | null)?.cause?.code ?? '');
}

export const internalApiInterceptor: HttpInterceptorFn = (req, next) => {
  const internalBase = inject(INTERNAL_API_BASE_URL);
  const url = toInternalApiUrl(req.url, internalBase);
  if (url === req.url) return next(req);
  const yanit = next(req.clone({ url, setHeaders: INTERNAL_API_HEADERS }));
  return req.method === 'GET' ? yanit.pipe(baglantiHatasindaYenidenDene()) : yanit;
};

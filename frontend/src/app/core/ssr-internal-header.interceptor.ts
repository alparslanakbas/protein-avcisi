import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

// SSR sırasında backend'e (api.proteinavcisi.com.tr) attığımız her istek
// Render'ın kendi çıkış IP'sinden gidiyor — Cloudflare Bot Fight Mode bunu
// bot trafiği sanıp engelliyordu (2026-08-15 olayı, Bot Fight Mode geçici
// olarak kapatılarak çözülmüştü). Bu interceptor SADECE app.config.server.ts
// üzerinden, sunucu tarafında sağlanıyor — tarayıcı bundle'ına hiç girmiyor,
// dolayısıyla gizli değer istemciye asla gitmiyor. Cloudflare'de bu header'a
// sahip istekler için Bot Fight Mode'u atlayan bir WAF kuralı kuruldu, böylece
// Bot Fight Mode genel ziyaretçi trafiği için tekrar açık kalabiliyor.
@Injectable()
export class SsrInternalHeaderInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const secret = process.env['SSR_INTERNAL_SECRET'];
    if (!secret) return next.handle(req);
    return next.handle(req.clone({ setHeaders: { 'X-Internal-Request': secret } }));
  }
}

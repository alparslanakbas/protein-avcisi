import { HttpErrorResponse } from '@angular/common/http';

// E-posta-hassas uçlar (favori ekleme, "Haber Ver", kurtarma linki, bülten
// aboneliği) hepsi aynı IP-bazlı rate limit'i paylaşıyor (backend'deki
// "EmailSensitive" policy, 5 dakikada 5 istek). Bu, kullanıcı testinde
// jenerik "bir şeyler ters gitti" mesajının arkasında kaybolmuştu — 429'u
// ayırt edip ne olduğunu açıkça söylüyoruz.
const RATE_LIMIT_MESSAGE = 'Kısa sürede çok fazla istek gönderdin, birkaç dakika sonra tekrar dener misin?';

export function friendlyErrorMessage(error: unknown, genericMessage = 'Bir şeyler ters gitti, tekrar dener misin?'): string {
  return error instanceof HttpErrorResponse && error.status === 429 ? RATE_LIMIT_MESSAGE : genericMessage;
}

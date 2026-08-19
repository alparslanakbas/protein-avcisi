import { HttpErrorResponse } from '@angular/common/http';

// E-posta-hassas uçlar (favori ekleme, "Haber Ver", kurtarma linki, bülten
// aboneliği) hepsi aynı IP-bazlı rate limit'i paylaşıyor (backend'deki
// "EmailSensitive" policy, 5 dakikada 5 istek). Bu, kullanıcı testinde
// jenerik "bir şeyler ters gitti" mesajının arkasında kaybolmuştu — 429'u
// ayırt edip ne olduğunu açıkça söylüyoruz.
const RATE_LIMIT_MESSAGE = 'Kısa sürede çok fazla istek gönderdin, birkaç dakika sonra tekrar dener misin?';

export function friendlyErrorMessage(error: unknown, genericMessage = 'Bir şeyler ters gitti, tekrar dener misin?'): string {
  if (!(error instanceof HttpErrorResponse)) return genericMessage;
  if (error.status === 429) return RATE_LIMIT_MESSAGE;

  // Backend zaten Türkçe, kullanıcı dostu bir { message: "..." } gövdesi
  // döndürüyorsa (ör. 400 doğrulama hatası, 502 "e-posta şu anda
  // gönderilemiyor") onu göstermek jenerik mesajdan her zaman daha
  // faydalı — backend'e yeni bir anlamlı hata eklendiğinde frontend'in
  // ayrıca güncellenmesi gerekmiyor.
  const backendMessage = (error.error as { message?: unknown } | null)?.message;
  return typeof backendMessage === 'string' && backendMessage.trim().length > 0 ? backendMessage : genericMessage;
}

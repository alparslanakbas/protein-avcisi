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

// Bu ekran şimdiye kadar birbirinden çok farklı üç sebebi tek bir "Bağlantı
// sorunu" mesajıyla gösteriyordu: hız sınırı (429), sunucu hatası (5xx) ve
// isteğin sunucuya hiç ulaşamaması. Kullanıcı açısından bunlar aynı şey
// değil; üstelik eski metin ("Fiyat bilgilerine ulaşamıyoruz") listenin
// KAYBOLDUĞU gibi okunabiliyordu. Bu yüzden her varyant, listenin sunucuda
// durduğunu açıkça söylüyor.
//
// Kod (HTTP 429/504 gibi) küçük puntoyla gösteriliyor: kullanıcıyı
// korkutmamalı ama sorun bildirildiğinde hangi durum olduğunu tahmin
// etmek zorunda kalmayalım.
export interface LoadErrorInfo {
  label: string;
  title: string;
  message: string;
  code: string | null;
}

export function describeLoadError(error: unknown): LoadErrorInfo {
  const status = error instanceof HttpErrorResponse ? error.status : null;

  if (status === 429) {
    return {
      label: 'Yoğunluk',
      title: 'Biraz hızlı gittik',
      message: 'Kısa sürede çok fazla istek gönderildi. Takip listen yerinde duruyor; birkaç saniye içinde kendiliğinden yeniden deneniyor.',
      code: 'HTTP 429',
    };
  }

  // Angular ağ seviyesindeki başarısızlıklarda (çevrimdışı, DNS, engelleyici
  // eklenti) status olarak 0 veriyor — sunucu hiç yanıt vermemiş demek.
  if (status === null || status === 0) {
    return {
      label: 'Bağlantı',
      title: 'İnternete bağlanılamadı',
      message: 'Takip listen sunucuda güvende. Bağlantını kontrol edip yeniden dene.',
      code: null,
    };
  }

  if (status >= 500) {
    return {
      label: 'Geçici aksaklık',
      title: 'Sunucumuzda geçici bir sorun var',
      message: 'Bu bizden kaynaklanıyor, takip listenden hiçbir şey kaybolmadı. Birazdan yeniden dener misin?',
      code: `HTTP ${status}`,
    };
  }

  return {
    label: 'Beklenmedik durum',
    title: 'Takip listen şu anda açılamadı',
    message: 'Listen sunucuda duruyor. Yeniden denediğinde büyük ihtimalle açılacak.',
    code: `HTTP ${status}`,
  };
}

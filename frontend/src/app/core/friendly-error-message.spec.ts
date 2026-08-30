import { HttpErrorResponse } from '@angular/common/http';

import { describeLoadError, friendlyErrorMessage } from './friendly-error-message';

describe('friendlyErrorMessage', () => {
  it('429 için hız sınırı mesajı döndürür', () => {
    expect(friendlyErrorMessage(new HttpErrorResponse({ status: 429 }))).toContain('çok fazla istek');
  });

  it('backend Türkçe bir mesaj döndürdüyse onu kullanır', () => {
    const hata = new HttpErrorResponse({ status: 400, error: { message: 'Geçerli bir e-posta adresi girin.' } });
    expect(friendlyErrorMessage(hata)).toBe('Geçerli bir e-posta adresi girin.');
  });

  it('HTTP olmayan hatada jenerik mesaja düşer', () => {
    expect(friendlyErrorMessage(new Error('patladı'), 'yedek mesaj')).toBe('yedek mesaj');
  });
});

// Takip listesi hata paneli, birbirinden çok farklı üç sebebi tek bir
// "Bağlantı sorunu" ekranıyla gösteriyordu. Kullanıcı bunun listenin
// kaybolduğu şeklinde okunabildiğini bildirdi; her varyantın listenin
// durduğunu söylemesi bu yüzden bir GEREKSİNİM, süs değil.
describe('describeLoadError', () => {
  it('429 u geçici yoğunluk olarak anlatır ve kodu verir', () => {
    const bilgi = describeLoadError(new HttpErrorResponse({ status: 429 }));
    expect(bilgi.code).toBe('HTTP 429');
    expect(bilgi.title).not.toBe('');
    expect(bilgi.message).toContain('yerinde');
  });

  it('sunucu hatasında bunun bizden kaynaklandığını söyler', () => {
    const bilgi = describeLoadError(new HttpErrorResponse({ status: 504 }));
    expect(bilgi.code).toBe('HTTP 504');
    expect(bilgi.message).toContain('bizden');
  });

  it('500 ve 503 de sunucu hatası dalına düşer', () => {
    expect(describeLoadError(new HttpErrorResponse({ status: 500 })).code).toBe('HTTP 500');
    expect(describeLoadError(new HttpErrorResponse({ status: 503 })).code).toBe('HTTP 503');
  });

  it('ağ hatasında (status 0) kod göstermez', () => {
    // Angular, istek sunucuya hiç ulaşamadığında status 0 veriyor. Kullanıcıya
    // gösterilecek anlamlı bir kod yok.
    const bilgi = describeLoadError(new HttpErrorResponse({ status: 0 }));
    expect(bilgi.code).toBeNull();
    expect(bilgi.title).toContain('bağlan');
  });

  it('HTTP olmayan hatada da kod göstermez', () => {
    expect(describeLoadError(new Error('beklenmedik')).code).toBeNull();
  });

  it('diğer durumlarda kodu yine de iletir', () => {
    expect(describeLoadError(new HttpErrorResponse({ status: 418 })).code).toBe('HTTP 418');
  });

  it('HER varyant listenin durduğunu söyler', () => {
    // Asıl gereksinim bu: kullanıcı hiçbir varyantta "listem silindi"
    // sonucuna varmamalı.
    const varyantlar = [
      describeLoadError(new HttpErrorResponse({ status: 429 })),
      describeLoadError(new HttpErrorResponse({ status: 504 })),
      describeLoadError(new HttpErrorResponse({ status: 0 })),
      describeLoadError(new HttpErrorResponse({ status: 418 })),
    ];
    for (const v of varyantlar) {
      const metin = `${v.title} ${v.message}`.toLocaleLowerCase('tr');
      expect(metin).toContain('listen');
    }
  });
});

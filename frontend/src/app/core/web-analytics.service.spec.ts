import { TestBed } from '@angular/core/testing';

import { CookieConsentService } from './cookie-consent.service';
import { CLOUDFLARE_BEACON_TOKEN, WebAnalyticsService } from './web-analytics.service';

function beaconSayisi(): number {
  return document.head.querySelectorAll('script[data-cf-beacon]').length;
}

function kur(jeton: string) {
  TestBed.configureTestingModule({
    providers: [{ provide: CLOUDFLARE_BEACON_TOKEN, useValue: jeton }],
  });
  const consent = TestBed.inject(CookieConsentService);
  TestBed.inject(WebAnalyticsService);
  return consent;
}

describe('WebAnalyticsService', () => {
  beforeEach(() => {
    localStorage.removeItem('cookie-consent');
    document.head.querySelectorAll('script[data-cf-beacon]').forEach((el) => el.remove());
    TestBed.resetTestingModule();
  });

  // ASIL KORUMA. Çerez bandımız kullanıcıya "reklam veya analiz
  // etkinleştirilirse bu seçimine uyacağız" diyor. Cloudflare'in ürünü
  // çerezsiz olduğu için yasal olarak onay gerekmeyebilir, ama verilmiş bir
  // söz var — ve bu sitenin bütün iddiası söylenenle yapılan arasındaki
  // farkı göstermek.
  it('onay verilmemişken beacon YÜKLENMİYOR', () => {
    const consent = kur('jeton123');
    TestBed.flushEffects();
    expect(consent.status()).toBeNull();
    expect(beaconSayisi()).toBe(0);
  });

  it('onay REDDEDİLMİŞSE beacon yüklenmiyor', () => {
    const consent = kur('jeton123');
    consent.reject();
    TestBed.flushEffects();
    expect(beaconSayisi()).toBe(0);
  });

  it('onay verilince beacon yükleniyor', () => {
    const consent = kur('jeton123');
    consent.accept();
    TestBed.flushEffects();
    expect(beaconSayisi()).toBe(1);
  });

  // Yarım yapılandırmayla sessizce ölçüm yapmasın: jeton yoksa özellik kapalı.
  it('jeton boşsa onay verilse bile yüklenmiyor', () => {
    const consent = kur('');
    consent.accept();
    TestBed.flushEffects();
    expect(beaconSayisi()).toBe(0);
  });

  it('onay geri alınırsa script DOM\'dan çıkıyor', () => {
    const consent = kur('jeton123');
    consent.accept();
    TestBed.flushEffects();
    expect(beaconSayisi()).toBe(1);

    consent.reject();
    TestBed.flushEffects();
    expect(beaconSayisi()).toBe(0);
  });

  it('iki kez onay verilse de tek script ekleniyor', () => {
    const consent = kur('jeton123');
    consent.accept();
    TestBed.flushEffects();
    consent.accept();
    TestBed.flushEffects();
    expect(beaconSayisi()).toBe(1);
  });
});

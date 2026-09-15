import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { PageMetaService } from '../core/page-meta.service';
import { YonetimPage } from './yonetim-page';
import { YonetimService } from './yonetim.service';

describe('YonetimPage görünürlük güvenliği', () => {
  const api = {
    markaDurumuGuncelle: vi.fn(() => of({ id: 12, name: 'Örnek marka', isActive: false })),
    urunDurumuGuncelle: vi.fn(() => of({ id: 34, name: 'Örnek ürün', isActive: false })),
    markalar: vi.fn(() => of([])),
    urunler: vi.fn(() => of([])),
    aboneler: vi.fn(() =>
      of({ aboneler: [], ozet: { toplam: 0, aktif: 0, bekleyen: 0, ayrilan: 0 } }),
    ),
    abonePasifeAl: vi.fn(() => of({})),
    aboneOnayGonder: vi.fn(() => of({ message: 'gönderildi' })),
  };

  const abone = {
    id: 7,
    email: 'okur@example.com',
    durum: 'aktif' as const,
    subscribedAt: '2026-09-13T02:40:51Z',
    confirmedAt: '2026-09-13T16:45:22Z',
    unsubscribedAt: null,
    lastConfirmationEmailSentAt: null,
    lastDigestSentAt: null,
    takipSayisi: 2,
    favoriSayisi: 0,
  };

  let sayfa: YonetimPage;

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        { provide: YonetimService, useValue: api },
        { provide: PageMetaService, useValue: { set: vi.fn() } },
        { provide: PLATFORM_ID, useValue: 'server' },
      ],
    });
    sayfa = TestBed.runInInjectionContext(() => new YonetimPage());
  });

  it('gözlem dönemi sürerken yayındaki öğeyi gizletmez', () => {
    sayfa.gorunurlukKilitli.set(true);

    sayfa.gorunurlukDegisikligiIste('marka', 12, 'Örnek marka', true);

    expect(sayfa.bekleyenDegisiklik()).toBeNull();
    expect(api.markaDurumuGuncelle).not.toHaveBeenCalled();
    expect(sayfa.gorunurlukMesaji()).toContain('kilitli');
  });

  it('kilit kalktığında gizleme için onay ister ve onaydan sonra günceller', () => {
    sayfa.gorunurlukKilitli.set(false);

    sayfa.gorunurlukDegisikligiIste('marka', 12, 'Örnek marka', true);

    expect(sayfa.bekleyenDegisiklik()).toEqual({
      tip: 'marka',
      id: 12,
      name: 'Örnek marka',
    });
    expect(api.markaDurumuGuncelle).not.toHaveBeenCalled();

    sayfa.degisikligiOnayla();

    expect(api.markaDurumuGuncelle).toHaveBeenCalledWith(12, false);
    expect(sayfa.bekleyenDegisiklik()).toBeNull();
    expect(sayfa.gorunurlukMesaji()).toBe('Örnek marka gizlendi.');
  });

  it('aboneyi pasife almadan önce sorar, yalnızca onaydan sonra pasife alır', () => {
    sayfa.pasifeAlmaIste(abone);

    expect(sayfa.bekleyenPasifeAlma()).toEqual(abone);
    expect(api.abonePasifeAl).not.toHaveBeenCalled();

    sayfa.pasifeAlmaOnayla();

    expect(api.abonePasifeAl).toHaveBeenCalledWith(7);
    expect(sayfa.bekleyenPasifeAlma()).toBeNull();
    expect(api.aboneler).toHaveBeenCalled();
  });

  // Bekleme süresi dolmadan tekrar gönderilince backend'in KENDİ cümlesi
  // gösterilmeli; genel "gönderilemedi" hangi durumun olduğunu gizlerdi.
  it('onay e-postası bekleme süresindeyse backend mesajını gösterir', () => {
    api.aboneOnayGonder.mockReturnValueOnce(
      throwError(() => ({
        status: 429,
        error: { message: 'Son 5 dakika içinde zaten bir onay e-postası gönderildi.' },
      })),
    );

    sayfa.onayGonder({ ...abone, durum: 'bekliyor', confirmedAt: null });

    expect(sayfa.aboneMesaji()).toBe('Son 5 dakika içinde zaten bir onay e-postası gönderildi.');
    expect(sayfa.aboneIslemdeId()).toBeNull();
  });
});

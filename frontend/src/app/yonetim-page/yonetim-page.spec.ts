import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
});

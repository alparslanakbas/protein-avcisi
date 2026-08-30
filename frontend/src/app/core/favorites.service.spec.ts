import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { FavoritesService } from './favorites.service';

// Bu testlerin sebebi gerçek bir üretim sorunu: rozeti gösteren üç bileşen
// (site-header, mobile-tab-bar, deals-list) sayaç için ayrı ayrı list()
// çağırıyordu. Sayaç servis seviyesinde paylaşılıyordu ama İSTEK
// paylaşılmıyordu; tek sayfa açılışında 2-3 istek gidiyor ve hız sınırına
// takılıyordu.

describe('FavoritesService.ensureCount', () => {
  let servis: FavoritesService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Servis isPlatformBrowser'a bakıyor; tarayıcı olmadan hiç istek atmıyor.
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });
    servis = TestBed.inject(FavoritesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('token yoksa hiç istek atmaz ve sayacı sıfırlar', () => {
    servis.count.set(5);
    servis.ensureCount();

    http.expectNone((r) => r.url.includes('/api/favorites'));
    expect(servis.count()).toBe(0);
  });

  it('REGRESYON: art arda çağrılsa da TEK istek atar', () => {
    localStorage.setItem('favorites-token', 'test-token');

    // Üç bileşenin ngOnInit'i aynı tick içinde çalışıyor; bayrak
    // subscribe'dan önce set edilmezse üçü de istek atardı.
    servis.ensureCount();
    servis.ensureCount();
    servis.ensureCount();

    const istekler = http.match((r) => r.url.includes('/api/favorites'));
    expect(istekler.length).toBe(1);
    istekler[0].flush([{ productId: 1 }, { productId: 2 }]);
    expect(servis.count()).toBe(2);
  });

  it('istek başarılıysa sonraki çağrılar ağa çıkmaz', () => {
    localStorage.setItem('favorites-token', 'test-token');

    servis.ensureCount();
    http.match((r) => r.url.includes('/api/favorites'))[0].flush([{ productId: 1 }]);

    servis.ensureCount();
    http.expectNone((r) => r.url.includes('/api/favorites'));
    expect(servis.count()).toBe(1);
  });

  it('istek başarısızsa bir sonraki çağrıda tekrar denenir', () => {
    localStorage.setItem('favorites-token', 'test-token');

    servis.ensureCount();
    http
      .match((r) => r.url.includes('/api/favorites'))[0]
      .flush('hata', { status: 500, statusText: 'Server Error' });

    // Bayrak geri alınmalı, yoksa sayaç oturum boyunca hiç yüklenmezdi.
    servis.ensureCount();
    expect(http.match((r) => r.url.includes('/api/favorites')).length).toBe(1);
  });

  it('signOut sonrası sayaç yeniden çekilebilir', () => {
    localStorage.setItem('favorites-token', 'test-token');
    servis.ensureCount();
    http.match((r) => r.url.includes('/api/favorites'))[0].flush([{ productId: 1 }]);

    servis.signOut();
    expect(servis.count()).toBe(0);

    // Kurtarma bağlantısıyla yeni bir token geldiğinde istek yeniden atılmalı.
    servis.saveToken('kurtarilan-token');
    servis.ensureCount();
    expect(http.match((r) => r.url.includes('/api/favorites')).length).toBe(1);
  });
});

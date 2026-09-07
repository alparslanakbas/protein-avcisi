import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { YonetimService } from './yonetim.service';

describe('YonetimService görünürlük uçları', () => {
  let servis: YonetimService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    servis = TestBed.inject(YonetimService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('markaları API alt alanı yerine korunan aynı-origin yolundan alır', () => {
    servis.markalar().subscribe();
    const istek = http.expectOne('/yonetim/api/markalar');
    expect(istek.request.method).toBe('GET');
    istek.flush([]);
  });

  it('ürün aramasını ve gizli filtresini URL üzerinde doğru kodlar', () => {
    servis.urunler('whey protein', true).subscribe();
    const istek = http.expectOne('/yonetim/api/urunler?ara=whey+protein&yalnizGizli=true');
    expect(istek.request.method).toBe('GET');
    istek.flush([]);
  });

  it('marka ve ürün durumunu yalnız isActive gövdesiyle günceller', () => {
    servis.markaDurumuGuncelle(12, false).subscribe();
    const markaIstegi = http.expectOne('/yonetim/api/markalar/12');
    expect(markaIstegi.request.method).toBe('PUT');
    expect(markaIstegi.request.body).toEqual({ isActive: false });
    markaIstegi.flush({ id: 12, name: 'Örnek', isActive: false });

    servis.urunDurumuGuncelle(34, true).subscribe();
    const urunIstegi = http.expectOne('/yonetim/api/urunler/34');
    expect(urunIstegi.request.method).toBe('PUT');
    expect(urunIstegi.request.body).toEqual({ isActive: true });
    urunIstegi.flush({ id: 34, name: 'Örnek ürün', isActive: true });
  });
});

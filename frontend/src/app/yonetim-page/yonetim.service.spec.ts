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
    istek.flush({ urunler: [], toplam: 0, sayfa: 1, sayfaBoyutu: 50 });
  });

  it('veri filtrelerini ve sayfayı backend parametre adlarıyla gönderir', () => {
    servis.urunler('', false, true, true, true, true, 3).subscribe();
    const istek = http.expectOne(
      '/yonetim/api/urunler?eksikBesin=true&kategorisiz=true&elleGirilmeli=true&elleGirilmis=true&sayfa=3',
    );
    istek.flush({ urunler: [], toplam: 0, sayfa: 3, sayfaBoyutu: 50 });
  });

  it('kategori ve besin değerini ürünün alt yollarına yazar', () => {
    servis.kategoriAyarla(21, null).subscribe();
    const kategori = http.expectOne('/yonetim/api/urunler/21/kategori');
    expect(kategori.request.method).toBe('PUT');
    expect(kategori.request.body).toEqual({ kategori: null });
    kategori.flush({ guncellenenSatir: 1 });

    servis.besinTemizle(21).subscribe();
    const temizle = http.expectOne('/yonetim/api/urunler/21/besin');
    expect(temizle.request.method).toBe('DELETE');
    temizle.flush({ guncellenenSatir: 1 });
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

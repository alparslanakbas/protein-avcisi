import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { defer, throwError, of, firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { API_BASE_URL } from './api.config';
import {
  BAGLANTI_YENIDEN_DENEME,
  INTERNAL_API_BASE_URL,
  baglantiHatasindaYenidenDene,
  baglantiKurulamadi,
  internalApiInterceptor,
  toInternalApiUrl,
} from './internal-api';

describe('toInternalApiUrl', () => {
  it('iç adres yoksa dokunmuyor (yerel geliştirme, tarayıcı)', () => {
    expect(toInternalApiUrl(`${API_BASE_URL}/api/deals`, null)).toBe(`${API_BASE_URL}/api/deals`);
  });

  it('API adresinin önekini iç adresle değiştiriyor, yol ve sorgu korunuyor', () => {
    expect(toInternalApiUrl(`${API_BASE_URL}/api/deals?page=2`, 'http://backend:8080/')).toBe(
      'http://backend:8080/api/deals?page=2',
    );
  });

  it('başka adreslere ve öneki yalnızca benzeyen hostlara dokunmuyor', () => {
    expect(toInternalApiUrl('https://example.com/api/deals', 'http://backend:8080')).toBe('https://example.com/api/deals');
    expect(toInternalApiUrl(`${API_BASE_URL}.evil.test/x`, 'http://backend:8080')).toBe(`${API_BASE_URL}.evil.test/x`);
    expect(toInternalApiUrl('/yonetim/api/durum', 'http://backend:8080')).toBe('/yonetim/api/durum');
  });
});

// Deploy anında backend birkaç saniye bağlantı kabul etmiyor (27 Ekim).
describe('baglantiHatasindaYenidenDene', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const baglantiHatasi = () => new HttpErrorResponse({ status: 0, statusText: 'Unknown Error' });

  it('bağlantı hatasından sonra backend dönünce yanıtı veriyor', async () => {
    let deneme = 0;
    const kaynak = defer(() => (++deneme <= 2 ? throwError(baglantiHatasi) : of('veri')));

    const sonuc = firstValueFrom(kaynak.pipe(baglantiHatasindaYenidenDene(5, 500)));
    await vi.advanceTimersByTimeAsync(1000);

    await expect(sonuc).resolves.toBe('veri');
    expect(deneme).toBe(3);
  });

  it('backend gerçek bir yanıt verdiyse (500) tekrar denemiyor', async () => {
    let deneme = 0;
    const kaynak = defer(() => {
      deneme++;
      return throwError(() => new HttpErrorResponse({ status: 500 }));
    });

    await expect(firstValueFrom(kaynak.pipe(baglantiHatasindaYenidenDene(5, 500)))).rejects.toMatchObject({ status: 500 });
    expect(deneme).toBe(1);
  });

  it('backend hiç dönmezse üst sınırda hatayı veriyor (arıza gizlenmiyor)', async () => {
    let deneme = 0;
    const kaynak = defer(() => {
      deneme++;
      return throwError(baglantiHatasi);
    });

    const sonuc = firstValueFrom(kaynak.pipe(baglantiHatasindaYenidenDene(3, 500)));
    const beklenen = expect(sonuc).rejects.toMatchObject({ status: 0 });
    await vi.advanceTimersByTimeAsync(1500);

    await beklenen;
    expect(deneme).toBe(4);
  });

  it('üretim sınırı ~15 sn (Docker 10 sn durdurma + 2,3 sn açılış)', () => {
    expect(BAGLANTI_YENIDEN_DENEME.adet * BAGLANTI_YENIDEN_DENEME.araMs).toBe(15_000);
  });
});

describe('baglantiKurulamadi (server.ts apiFetch)', () => {
  const fetchHatasi = (code: string) => new TypeError('fetch failed', { cause: Object.assign(new Error(code), { code }) });

  it('istek backend\'e hiç ulaşmadıysa true', () => {
    expect(baglantiKurulamadi(fetchHatasi('ECONNREFUSED'))).toBe(true);
    expect(baglantiKurulamadi(fetchHatasi('ENOTFOUND'))).toBe(true);
    expect(baglantiKurulamadi(fetchHatasi('EAI_AGAIN'))).toBe(true);
  });

  it('yarıda kopan bağlantı ve diğer hatalar false (/go tıklaması iki kez sayılmasın)', () => {
    expect(baglantiKurulamadi(fetchHatasi('ECONNRESET'))).toBe(false);
    expect(baglantiKurulamadi(new Error('başka'))).toBe(false);
    expect(baglantiKurulamadi(null)).toBe(false);
  });
});

describe('internalApiInterceptor', () => {
  const ic = 'http://backend:8080';
  let http: HttpClient;
  let kontrol: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([internalApiInterceptor])),
        provideHttpClientTesting(),
        { provide: INTERNAL_API_BASE_URL, useValue: ic },
      ],
    });
    http = TestBed.inject(HttpClient);
    kontrol = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    kontrol.verify();
    vi.useRealTimers();
  });

  it('iç adrese giden GET bağlantı hatasında YENİ bir istekle tekrar deneniyor', async () => {
    const sonuc = firstValueFrom(http.get<{ ok: boolean }>(`${API_BASE_URL}/api/stats`));

    kontrol.expectOne(`${ic}/api/stats`).error(new ProgressEvent('error'));
    await vi.advanceTimersByTimeAsync(BAGLANTI_YENIDEN_DENEME.araMs);
    const ikinci = kontrol.expectOne(`${ic}/api/stats`);
    expect(ikinci.request.headers.get('X-Forwarded-Proto')).toBe('https');
    ikinci.flush({ ok: true });

    await expect(sonuc).resolves.toEqual({ ok: true });
  });

  it('POST tekrar denenmiyor', async () => {
    const sonuc = firstValueFrom(http.post(`${API_BASE_URL}/api/subscribe`, {}));
    const beklenen = expect(sonuc).rejects.toMatchObject({ status: 0 });

    kontrol.expectOne(`${ic}/api/subscribe`).error(new ProgressEvent('error'));
    await vi.advanceTimersByTimeAsync(BAGLANTI_YENIDEN_DENEME.araMs * 2);

    await beklenen;
    kontrol.expectNone(`${ic}/api/subscribe`);
  });

  it('iç adres dışındaki isteğe dokunmuyor (tarayıcı yolu)', async () => {
    const sonuc = firstValueFrom(http.get('https://example.com/x'));
    const beklenen = expect(sonuc).rejects.toMatchObject({ status: 0 });

    kontrol.expectOne('https://example.com/x').error(new ProgressEvent('error'));
    await vi.advanceTimersByTimeAsync(BAGLANTI_YENIDEN_DENEME.araMs * 2);

    await beklenen;
    kontrol.expectNone('https://example.com/x');
  });
});

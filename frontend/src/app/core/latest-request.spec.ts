import { Subject } from 'rxjs';
import { LatestRequest } from './latest-request';

// Liste sayfalarındaki yarış: filtre hızlı değişince yavaş gelen ESKİ yanıt
// yenisinin üstüne yazabiliyordu.

describe('LatestRequest', () => {
  it('geç gelen eski yanıt yenisinin üstüne yazmıyor', () => {
    const latest = new LatestRequest();
    const yavas = new Subject<string>();
    const hizli = new Subject<string>();
    let ekrandaki = '';

    latest.run(yavas, { next: (v) => (ekrandaki = v) });
    latest.run(hizli, { next: (v) => (ekrandaki = v) });

    hizli.next('yeni süzgeç');
    yavas.next('eski süzgeç'); // sıra dışı, geç gelen yanıt

    expect(ekrandaki).toBe('yeni süzgeç');
  });

  it('önceki isteğin aboneliği kesiliyor (HTTP isteği iptal olur)', () => {
    const latest = new LatestRequest();
    const ilk = new Subject<string>();

    latest.run(ilk, {});
    expect(ilk.observed).toBe(true);

    latest.run(new Subject<string>(), {});
    expect(ilk.observed).toBe(false);
  });

  it('tek istekte hata da iletiliyor', () => {
    const latest = new LatestRequest();
    const istek = new Subject<string>();
    let hata = false;

    latest.run(istek, { error: () => (hata = true) });
    istek.error(new Error('ağ'));

    expect(hata).toBe(true);
  });
});

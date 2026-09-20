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
    urunler: vi.fn((..._args: unknown[]) =>
      of({ urunler: [] as unknown[], toplam: 0, sayfa: 1, sayfaBoyutu: 50 }),
    ),
    aboneler: vi.fn(() =>
      of({ aboneler: [], ozet: { toplam: 0, aktif: 0, bekleyen: 0, ayrilan: 0 } }),
    ),
    abonePasifeAl: vi.fn(() => of({})),
    aboneOnayGonder: vi.fn(() => of({ message: 'gönderildi' })),
    besinAyarla: vi.fn(() => of({ guncellenenSatir: 3 })),
    kategoriAyarla: vi.fn(() => of({ guncellenenSatir: 3 })),
    besinTemizle: vi.fn(() => of({ guncellenenSatir: 3 })),
  };

  // Canlıdaki bir otomatik okumanın yazımıyla: iki dilli adlar, virgüllü ondalık.
  const urun = {
    id: 21,
    name: 'Whey Protein 2300 gr',
    marka: 'Örnek',
    seller: null,
    isActive: true,
    latestPrice: 1999,
    category: 'protein-tozu',
    categoryIsManual: false,
    nutritionJson:
      '{"Enerji/ Energy":"498 kj / 119 kcal","Yağ / Fat":"1,9 g","Karbonhidrat / Carbohydrate":"1,4 g","Protein":"24,1 g","Tuz / Salt":"0,21 gr"}',
    nutritionIsManual: false,
    servingSizeGrams: 30,
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
  it('düzenleyiciyi iki dilli, virgüllü kayıtlı tablodan doldurur ve boşu null gönderir', () => {
    sayfa.veriDuzenleyiciAc(urun);
    const form = sayfa.duzenlenenVeri()!;
    expect(form.enerji).toBe('119');
    expect(form.yag).toBe('1.9');
    expect(form.protein).toBe('24.1');
    expect(form.digerSatirlar).toEqual([{ ad: 'Tuz / Salt', miktar: '0.21', birim: 'g' }]);

    sayfa.besinKaydet();

    expect(api.besinAyarla).toHaveBeenCalledWith(21, {
      porsiyonGram: 30,
      kalori: 119,
      proteinGram: 24.1,
      karbonhidratGram: 1.4,
      yagGram: 1.9,
      lifGram: null,
      etiketBoyleYaziyor: false,
      digerSatirlar: [{ ad: 'Tuz / Salt', miktar: 0.21, birim: 'g' }],
    });
  });

  // Kreatin etiketi: makro yok, adlı satırlar. Düzenlenemeyen biçim sessizce
  // düşürülmüyor, adıyla söyleniyor.
  it('etken madde satırlarını geri okur, düzenlenemeyeni adıyla bildirir, yazılanı gönderir', () => {
    sayfa.veriDuzenleyiciAc({
      ...urun,
      category: 'kreatin',
      servingSizeGrams: 5,
      nutritionJson: '{"Porsiyon":"5 g","Kreatin Monohidrat":"5 g","Vitamin D3":"25 mcg","Demir":"%10"}',
    });

    expect(sayfa.duzenlenenVeri()!.digerSatirlar).toEqual([
      { ad: 'Kreatin Monohidrat', miktar: '5', birim: 'g' },
      { ad: 'Vitamin D3', miktar: '25', birim: 'mcg' },
    ]);
    expect(sayfa.veriMesaji()).toContain('Demir');

    sayfa.digerSatirSil(1);
    sayfa.digerSatirEkle();
    sayfa.digerSatirGuncelle(1, 'ad', 'Kafein');
    sayfa.digerSatirGuncelle(1, 'miktar', 200);
    sayfa.besinKaydet();

    expect(api.besinAyarla).toHaveBeenCalledWith(21, {
      porsiyonGram: 5,
      kalori: null,
      proteinGram: null,
      karbonhidratGram: null,
      yagGram: null,
      lifGram: null,
      etiketBoyleYaziyor: false,
      digerSatirlar: [
        { ad: 'Kreatin Monohidrat', miktar: 5, birim: 'g' },
        { ad: 'Kafein', miktar: 200, birim: 'mg' },
      ],
    });
  });

  it('kategori şablonunu bir kez ekler ve miktarsız satırları göndermez', () => {
    sayfa.veriDuzenleyiciAc({ ...urun, category: 'pre-workout', nutritionJson: null });
    expect(sayfa.sablonKategoriEtiketi(sayfa.duzenlenenVeri()!)).toBe('Pre-Workout');

    sayfa.sablonSatirlariEkle();
    expect(sayfa.duzenlenenVeri()!.digerSatirlar.map((s) => s.ad)).toEqual([
      'Kafein',
      'L-Sitrülin',
      'Beta Alanin',
      'Betain',
    ]);
    expect(sayfa.sablonKategoriEtiketi(sayfa.duzenlenenVeri()!)).toBeNull();

    sayfa.digerSatirGuncelle(0, 'miktar', '300');
    sayfa.besinKaydet();

    expect(api.besinAyarla).toHaveBeenCalledWith(
      21,
      expect.objectContaining({ digerSatirlar: [{ ad: 'Kafein', miktar: 300, birim: 'mg' }] }),
    );
  });

  // Tablo zaten "Şeker / Sugar" taşıyorsa şablon ikinci bir "Şekerler" eklememeli
  // (canlıdaki bir okumanın yazımı).
  it('şablon iki dilli adla kayıtlı satırı tekrar eklemez', () => {
    sayfa.veriDuzenleyiciAc({
      ...urun,
      nutritionJson: '{"Şeker / Sugar":"1 g","Doymuş yağ /Saturated Fat":"0,5 g","Tuz":"0,2 g"}',
    });

    expect(sayfa.sablonKategoriEtiketi(sayfa.duzenlenenVeri()!)).toBeNull();
  });

  it('backend reddederse sebebini gösterir', () => {
    api.besinAyarla.mockReturnValueOnce(
      throwError(() => ({
        status: 400,
        error: { message: 'Enerji 400 kcal makrolarla tutmuyor.' },
      })),
    );
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.veriAlaniGuncelle('enerji', 400);

    sayfa.besinKaydet();

    expect(sayfa.veriMesaji()).toBe('Enerji 400 kcal makrolarla tutmuyor.');
    expect(sayfa.veriKaydediliyor()).toBe(false);
  });

  it('otomatik kategoriye dönmek için null gönderir', () => {
    sayfa.veriDuzenleyiciAc({ ...urun, categoryIsManual: true, category: 'vitamin' });
    sayfa.veriAlaniGuncelle('kategori', '');

    sayfa.kategoriKaydet();

    expect(api.kategoriAyarla).toHaveBeenCalledWith(21, null);
  });

  // "Besin değeri eksik" binlerce satıra uyuyor; liste 200'de kesilirdi.
  it('ürün listesini sayfalar ve düzenlemeden sonra sayfayı korur', () => {
    api.urunler.mockImplementation((...args: unknown[]) =>
      of({
        urunler: [urun] as unknown[],
        toplam: 1234,
        sayfa: (args[5] as number) ?? 1,
        sayfaBoyutu: 50,
      }),
    );

    sayfa.veriFiltresiDegisti('eksikBesin', true);
    expect(api.urunler).toHaveBeenLastCalledWith('', false, true, false, false, 1);
    expect(sayfa.urunToplamSayfa()).toBe(25);

    sayfa.urunSayfasinaGit(3);
    expect(api.urunler).toHaveBeenLastCalledWith('', false, true, false, false, 3);
    expect(sayfa.urunAraligiBaslangic()).toBe(101);

    sayfa.urunSayfasinaGit(99);
    expect(api.urunler).toHaveBeenLastCalledWith('', false, true, false, false, 25);

    sayfa.urunSayfasinaGit(3);
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.kategoriKaydet();
    expect(api.urunler).toHaveBeenLastCalledWith('', false, true, false, false, 3);

    sayfa.veriFiltresiDegisti('kategorisiz', true);
    expect(api.urunler).toHaveBeenLastCalledWith('', false, true, true, false, 1);
  });

  it('elle girilmeli listesini aramasız ister', () => {
    sayfa.veriFiltresiDegisti('elleGirilmeli', true);

    expect(api.urunler).toHaveBeenLastCalledWith('', false, false, false, true, 1);
    expect(sayfa.urunAramaYapildi()).toBe(true);
  });

  it('bakılan sayfa boşaldıysa son sayfaya döner', () => {
    api.urunler.mockImplementation((...args: unknown[]) => {
      const istenen = (args[5] as number) ?? 1;
      return of({
        urunler: (istenen > 2 ? [] : [urun]) as unknown[],
        toplam: 60,
        sayfa: istenen,
        sayfaBoyutu: 50,
      });
    });

    sayfa.veriFiltresiDegisti('eksikBesin', true);
    sayfa.urunAra(3);

    expect(api.urunler).toHaveBeenLastCalledWith('', false, true, false, false, 2);
    expect(sayfa.urunSayfa()).toBe(2);
  });
});

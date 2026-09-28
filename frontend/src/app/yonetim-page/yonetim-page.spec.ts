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
    servingsPerPackage: null,
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
      porsiyonAdedi: null,
      porsiyonBirimi: null,
      porsiyonMl: null,
      kalori: 119,
      proteinGram: 24.1,
      karbonhidratGram: 1.4,
      yagGram: 1.9,
      lifGram: null,
      etiketBoyleYaziyor: false,
      paketPorsiyonSayisi: null,
      porsiyonBeyanYok: false,
      digerSatirlar: [{ ad: 'Tuz / Salt', miktar: 0.21, birim: 'g' }],
    });
  });

  // Porsiyon beyanı olmayan etiket ("50 g başına"): yeniden açınca taban ve kutu
  // geri gelmeli, yoksa ikinci kayıt tabanı sessizce porsiyona çevirirdi.
  it('gram tabanlı tabloyu tabanı ve kutusuyla geri okur, aynen gönderir', () => {
    sayfa.veriDuzenleyiciAc({
      ...urun,
      servingSizeGrams: null,
      nutritionJson:
        '{"Değerler":"50 g başına","Enerji":"224,5 kcal","Yağ":"16,7 g","Karbonhidrat":"15,5 g","Protein":"15,1 g"}',
    });
    const form = sayfa.duzenlenenVeri()!;
    expect(form.porsiyon).toBe('50');
    expect(form.porsiyonBeyanYok).toBe(true);
    expect(form.digerSatirlar).toEqual([]);

    sayfa.besinKaydet();

    expect(api.besinAyarla).toHaveBeenCalledWith(
      21,
      expect.objectContaining({ porsiyonGram: 50, porsiyonBeyanYok: true, kalori: 224.5 }),
    );
  });

  // Kreatin etiketi: makro yok, adlı satırlar. Düzenlenemeyen biçim sessizce
  // düşürülmüyor, adıyla söyleniyor.
  it('etken madde satırlarını geri okur, düzenlenemeyeni adıyla bildirir, yazılanı gönderir', () => {
    sayfa.veriDuzenleyiciAc({
      ...urun,
      category: 'kreatin',
      servingSizeGrams: 5,
    servingsPerPackage: null,
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
      porsiyonAdedi: null,
      porsiyonBirimi: null,
      porsiyonMl: null,
      kalori: null,
      proteinGram: null,
      karbonhidratGram: null,
      yagGram: null,
      lifGram: null,
      etiketBoyleYaziyor: false,
      paketPorsiyonSayisi: null,
      porsiyonBeyanYok: false,
      digerSatirlar: [
        { ad: 'Kreatin Monohidrat', miktar: 5, birim: 'g' },
        { ad: 'Kafein', miktar: 200, birim: 'mg' },
      ],
    });
  });

  // Kapsüllü elektrolit (28 Eylül): etiketin porsiyonu "1 kapsül", gramını
  // yazmıyor; gram gönderilmiyor.
  it('porsiyon kutusuna yazılan kapsül porsiyonunu gramsız gönderir', () => {
    sayfa.veriDuzenleyiciAc({ ...urun, servingSizeGrams: null, nutritionJson: null });
    sayfa.veriAlaniGuncelle('porsiyon', '1 Kapsül');
    sayfa.digerSatirEkle();
    sayfa.digerSatirGuncelle(0, 'ad', 'Sodyum');
    sayfa.digerSatirGuncelle(0, 'miktar', 141);
    sayfa.besinKaydet();

    expect(api.besinAyarla).toHaveBeenCalledWith(
      21,
      expect.objectContaining({ porsiyonGram: null, porsiyonAdedi: 1, porsiyonBirimi: 'kapsül' }),
    );
  });

  it('kayıtlı sayılı porsiyonu gramıyla, yazıldığı gibi geri okur', () => {
    sayfa.veriDuzenleyiciAc({
      ...urun,
      servingSizeGrams: 1.2,
      nutritionJson: '{"Porsiyon":"2 tablet (1,2 g)","Kafein":"200 mg"}',
    });
    expect(sayfa.duzenlenenVeri()?.porsiyon).toBe('2 tablet (1,2 g)');

    sayfa.besinKaydet();
    expect(api.besinAyarla).toHaveBeenCalledWith(
      21,
      expect.objectContaining({ porsiyonGram: 1.2, porsiyonAdedi: 2, porsiyonBirimi: 'tablet' }),
    );
  });

  // 226ERS Sea Water: değerler 20 ml için. ml, ml olarak kalıyor; grama çevrilmiyor.
  it('sıvı porsiyonunu ml olarak gönderir', () => {
    sayfa.veriDuzenleyiciAc({ ...urun, servingSizeGrams: null, nutritionJson: null });
    sayfa.veriAlaniGuncelle('porsiyon', '20 ml');
    sayfa.digerSatirEkle();
    sayfa.digerSatirGuncelle(0, 'ad', 'Sodyum');
    sayfa.digerSatirGuncelle(0, 'miktar', 141);
    sayfa.besinKaydet();

    expect(api.besinAyarla).toHaveBeenCalledWith(
      21,
      expect.objectContaining({ porsiyonGram: null, porsiyonMl: 20, porsiyonAdedi: null }),
    );
  });

  // İçecek etiketi "100 ml başına": geri okununca "100" olsaydı ikinci kayıt tabanı
  // sessizce "100 g başına"ya çevirirdi.
  it('ml tabanlı tabloyu ml olarak geri okur ve aynen gönderir', () => {
    sayfa.veriDuzenleyiciAc({
      ...urun,
      servingSizeGrams: null,
      nutritionJson: '{"Değerler":"100 ml başına","Enerji":"45 kcal","Yağ":"0 g","Karbonhidrat":"11 g","Protein":"0 g"}',
    });
    const form = sayfa.duzenlenenVeri()!;
    expect(form.porsiyon).toBe('100 ml');
    expect(form.porsiyonBeyanYok).toBe(true);

    sayfa.besinKaydet();
    expect(api.besinAyarla).toHaveBeenCalledWith(
      21,
      expect.objectContaining({ porsiyonGram: null, porsiyonMl: 100, porsiyonBeyanYok: true }),
    );
  });

  it('okuyamadığı porsiyonu göndermez, virgüllü gramı okur', () => {
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.veriAlaniGuncelle('porsiyon', '2 ölçek');
    sayfa.besinKaydet();
    expect(api.besinAyarla).not.toHaveBeenCalled();
    expect(sayfa.veriMesaji()).toContain('Porsiyon');

    sayfa.veriAlaniGuncelle('porsiyon', '30,5');
    sayfa.besinKaydet();
    expect(api.besinAyarla).toHaveBeenCalledWith(21, expect.objectContaining({ porsiyonGram: 30.5 }));
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

  // Düzenleyicinin dışına tıklamak onu kapatıyor; yarım girdiler kaybolmamalı.
  it('kaydetmeden kapatılan girdileri yeniden açınca geri getirir, başka ürüne taşımaz', () => {
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.veriAlaniGuncelle('protein', 30);
    sayfa.veriAlaniGuncelle('karbonhidrat', 5);
    sayfa.digerSatirEkle();
    sayfa.digerSatirGuncelle(1, 'ad', 'Kafein');
    sayfa.veriDuzenleyiciKapat();
    expect(sayfa.duzenlenenVeri()).toBeNull();

    sayfa.veriDuzenleyiciAc({ ...urun, id: 22 });
    expect(sayfa.duzenlenenVeri()!.protein).toBe('24.1');
    expect(sayfa.veriMesaji()).toBeNull();
    sayfa.veriDuzenleyiciKapat();

    sayfa.veriDuzenleyiciAc(urun);
    const form = sayfa.duzenlenenVeri()!;
    expect(form.protein).toBe('30');
    expect(form.karbonhidrat).toBe('5');
    expect(form.yag).toBe('1.9');
    expect(form.digerSatirlar.map((s) => s.ad)).toEqual(['Tuz / Salt', 'Kafein']);
    expect(sayfa.veriMesaji()).toContain('geri getirildi');
  });

  it('değişmeyen ya da kaydedilen girdiyi taslak saymaz', () => {
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.veriDuzenleyiciKapat();
    sayfa.veriDuzenleyiciAc(urun);
    expect(sayfa.veriMesaji()).toBeNull();

    sayfa.veriAlaniGuncelle('protein', 30);
    sayfa.besinKaydet();
    sayfa.veriDuzenleyiciKapat();

    // Taslak kalmadığı için form verilen ürün kaydından kuruluyor.
    sayfa.veriDuzenleyiciAc(urun);
    expect(sayfa.duzenlenenVeri()!.protein).toBe('24.1');
    expect(sayfa.veriMesaji()).toBeNull();
  });

  // Kategori ve besin ayrı düğmelerle kaydediliyor.
  it('yalnızca kategori kaydedilince besin girdisini taslak olarak tutar', () => {
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.veriAlaniGuncelle('protein', 30);
    sayfa.veriAlaniGuncelle('kategori', 'vitamin');
    sayfa.kategoriKaydet();
    sayfa.veriDuzenleyiciKapat();

    sayfa.veriDuzenleyiciAc({ ...urun, category: 'vitamin', categoryIsManual: true });
    expect(sayfa.duzenlenenVeri()!.protein).toBe('30');
    expect(sayfa.duzenlenenVeri()!.kategori).toBe('vitamin');
    expect(sayfa.veriMesaji()).toContain('geri getirildi');
  });

  it('reddedilen kaydın girdisini tutar, "etiket böyle yazıyor" onayını başka ürüne taşımaz', () => {
    api.besinAyarla.mockReturnValueOnce(
      throwError(() => ({
        status: 400,
        error: { message: 'Enerji 400 kcal makrolarla tutmuyor.', kod: 'enerji-tutmuyor' },
      })),
    );
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.veriAlaniGuncelle('enerji', 400);
    sayfa.besinKaydet();
    expect(sayfa.sonRetKodu()).toBe('enerji-tutmuyor');
    sayfa.etiketBoyleYaziyor.set(true);
    sayfa.veriDuzenleyiciKapat();

    sayfa.veriDuzenleyiciAc({ ...urun, id: 22 });
    expect(sayfa.sonRetKodu()).toBeNull();
    expect(sayfa.etiketBoyleYaziyor()).toBe(false);
    sayfa.veriDuzenleyiciKapat();

    sayfa.veriDuzenleyiciAc(urun);
    expect(sayfa.duzenlenenVeri()!.enerji).toBe('400');
  });

  // "Besin değeri eksik" binlerce satıra uyuyor; liste 200'de kesilirdi.
  it('ürün listesini sayfalar ve düzenlemeden sonra sayfayı korur', () => {
    api.urunler.mockImplementation((...args: unknown[]) =>
      of({
        urunler: [urun] as unknown[],
        toplam: 1234,
        sayfa: ((args[0] as { sayfa?: number }).sayfa) ?? 1,
        sayfaBoyutu: 50,
      }),
    );

    sayfa.veriFiltresiDegisti('eksikBesin', true);
    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: true, kategorisiz: false, elleGirilmeli: false, elleGirilmis: false, sayfa: 1 }),
    );
    expect(sayfa.urunToplamSayfa()).toBe(25);

    sayfa.urunSayfasinaGit(3);
    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: true, kategorisiz: false, elleGirilmeli: false, elleGirilmis: false, sayfa: 3 }),
    );
    expect(sayfa.urunAraligiBaslangic()).toBe(101);

    sayfa.urunSayfasinaGit(99);
    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: true, kategorisiz: false, elleGirilmeli: false, elleGirilmis: false, sayfa: 25 }),
    );

    sayfa.urunSayfasinaGit(3);
    sayfa.veriDuzenleyiciAc(urun);
    sayfa.kategoriKaydet();
    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: true, kategorisiz: false, elleGirilmeli: false, elleGirilmis: false, sayfa: 3 }),
    );

    sayfa.veriFiltresiDegisti('kategorisiz', true);
    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: true, kategorisiz: true, elleGirilmeli: false, elleGirilmis: false, sayfa: 1 }),
    );
  });

  it('elle girilmeli listesini aramasız ister', () => {
    sayfa.veriFiltresiDegisti('elleGirilmeli', true);

    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: false, kategorisiz: false, elleGirilmeli: true, elleGirilmis: false, sayfa: 1 }),
    );
    expect(sayfa.urunAramaYapildi()).toBe(true);
  });

  it('bakılan sayfa boşaldıysa son sayfaya döner', () => {
    api.urunler.mockImplementation((...args: unknown[]) => {
      const istenen = ((args[0] as { sayfa?: number }).sayfa) ?? 1;
      return of({
        urunler: (istenen > 2 ? [] : [urun]) as unknown[],
        toplam: 60,
        sayfa: istenen,
        sayfaBoyutu: 50,
      });
    });

    sayfa.veriFiltresiDegisti('eksikBesin', true);
    sayfa.urunAra(3);

    expect(api.urunler).toHaveBeenLastCalledWith(
      expect.objectContaining({ ara: '', yalnizGizli: false, eksikBesin: true, kategorisiz: false, elleGirilmeli: false, elleGirilmis: false, sayfa: 2 }),
    );
    expect(sayfa.urunSayfa()).toBe(2);
  });
});

import { describe, expect, it } from 'vitest';

import { ADSENSE_CLIENT, allowsAdsenseScript, ensureAdsenseScript } from './adsense-script';

describe('allowsAdsenseScript', () => {
  it('editoryal sayfalara izin verir', () => {
    for (const path of [
      '/',
      '/efsane-kasim',
      '/sozluk',
      '/rehber',
      '/rehber/kreatin-nasil-kullanilir',
      '/hesaplama',
      '/hesaplama/protein-ihtiyaci',
      '/hangi-takviye',
      '/hangi-takviye/kas-kazanimi',
    ]) {
      expect(allowsAdsenseScript(path), path).toBe(true);
    }
  });

  it('şablondan üretilen sayfalara izin vermez', () => {
    for (const path of [
      '/urun/15/hiq-high-pro-protein-bar-12-55g',
      '/urun-inceleme/15/hiq-high-pro-protein-bar-12-55g',
      '/karsilastir/bigjoy-vs-bite-more',
      '/karsilastir-urun/80-vs-467',
      '/marka/aegis/indirim-kodu',
      '/marka/aegis/protein-tozu',
      '/kategori/amino-asitler',
      '/en-ucuz-kreatin',
      '/hakkimizda',
      '/yonetim',
    ]) {
      expect(allowsAdsenseScript(path), path).toBe(false);
    }
  });

  it('önek benzeşmesini yakalamaz', () => {
    expect(allowsAdsenseScript('/rehberler')).toBe(false);
    expect(allowsAdsenseScript('/hesaplamalar')).toBe(false);
  });
});

describe('ensureAdsenseScript', () => {
  const scripts = (doc: Document) => doc.head.querySelectorAll('script[data-adsense]');

  it('izinli sayfada betiği bir kez ekler', () => {
    const doc = document.implementation.createHTMLDocument('t');
    ensureAdsenseScript(doc, '/rehber');
    ensureAdsenseScript(doc, '/');

    const found = scripts(doc);
    expect(found.length).toBe(1);
    expect(found[0].getAttribute('src')).toContain(`client=${ADSENSE_CLIENT}`);
    expect(found[0].hasAttribute('async')).toBe(true);
    expect(found[0].getAttribute('crossorigin')).toBe('anonymous');
  });

  it('izinsiz sayfada eklemez', () => {
    const doc = document.implementation.createHTMLDocument('t');
    ensureAdsenseScript(doc, '/urun/15/x');
    expect(scripts(doc).length).toBe(0);
  });
});

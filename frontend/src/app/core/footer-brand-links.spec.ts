import { showFooterBrandLinks } from './footer-brand-links';

describe('showFooterBrandLinks', () => {
  it('ÜRÜN sayfasında gösterilmez — sayfanın %39\'u bu listeydi', () => {
    expect(showFooterBrandLinks('/urun/1126/hiq-bcaa-390g')).toBe(false);
  });

  it('İNCELEME sayfasında da gösterilmez', () => {
    expect(showFooterBrandLinks('/urun-inceleme/1126/hiq-bcaa-390g')).toBe(false);
  });

  it('ana sayfada GÖSTERİLİR — orası listenin asıl yeri', () => {
    expect(showFooterBrandLinks('/')).toBe(true);
  });

  it('marka ve kategori sayfalarında GÖSTERİLİR', () => {
    // Google marka sayfalarını buradan bulmaya devam etmeli; ürün
    // sayfalarından kaldırmak bir giriş noktası kaybettirmemeli.
    expect(showFooterBrandLinks('/marka/hiq/protein-tozu')).toBe(true);
    expect(showFooterBrandLinks('/kategori/kreatin')).toBe(true);
  });

  it('markalar dizininde aynı listeyi footer\'da tekrarlamaz', () => {
    expect(showFooterBrandLinks('/markalar')).toBe(false);
  });

  it('adı benzeyen ama farklı olan yollar etkilenmez', () => {
    expect(showFooterBrandLinks('/urunler')).toBe(true);
    expect(showFooterBrandLinks('/karsilastir-urun/263-1126')).toBe(true);
  });
});

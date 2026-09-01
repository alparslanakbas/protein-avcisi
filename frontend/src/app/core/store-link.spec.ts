import { storeLinkTarget } from './store-link';

/**
 * Bu karar kullanıcıya "geri tuşuna basınca uygulamadan atıldım" olarak
 * yansıyordu; sebebi ölçülerek bulundu (yeni bağlamda geçmişte yalnızca
 * yönlendirme zinciri var, geri basınca bağlam kapanıyor).
 */
describe('storeLinkTarget', () => {
  const gercekMatchMedia = window.matchMedia;
  const gercekStandalone = (navigator as unknown as { standalone?: boolean }).standalone;

  function displayModeAyarla(eslesen: string | null) {
    window.matchMedia = ((sorgu: string) =>
      ({ matches: eslesen !== null && sorgu.includes(eslesen) }) as MediaQueryList) as typeof window.matchMedia;
  }

  afterEach(() => {
    window.matchMedia = gercekMatchMedia;
    (navigator as unknown as { standalone?: boolean }).standalone = gercekStandalone;
  });

  it('sunucuda _blank döner (window yok, HTML tarayıcı için üretiliyor)', () => {
    expect(storeLinkTarget(false)).toBe('_blank');
  });

  it('normal tarayıcıda _blank kalır — mağazaya bakarken sitemiz açık kalsın', () => {
    displayModeAyarla(null);
    expect(storeLinkTarget(true)).toBe('_blank');
  });

  it('REGRESYON: kurulu PWA (standalone) _self olmalı', () => {
    // _blank olsaydı geri tuşu uygulamayı kapatırdı: yeni bağlamın
    // geçmişinde yalnızca /go/{id} → 302 → mağaza zinciri var.
    displayModeAyarla('standalone');
    expect(storeLinkTarget(true)).toBe('_self');
  });

  it('tam ekran PWA da aynı davranır', () => {
    displayModeAyarla('fullscreen');
    expect(storeLinkTarget(true)).toBe('_self');
  });

  it("iOS'ta navigator.standalone da dikkate alınır", () => {
    displayModeAyarla(null);
    (navigator as unknown as { standalone?: boolean }).standalone = true;
    expect(storeLinkTarget(true)).toBe('_self');
  });
});

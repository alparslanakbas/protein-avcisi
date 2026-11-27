import { goHedefi } from './go-yonlendirme';

describe('goHedefi', () => {
  it('ürün adı varsa kanonik slug adresine gider', () => {
    expect(goHedefi('3435', 'bahs. Pea Protein Powder')).toBe('/urun/3435/bahs-pea-protein-powder');
  });

  it('ürün okunamazsa slug\'sız ürün adresine gider', () => {
    expect(goHedefi('42', null)).toBe('/urun/42');
  });

  it('slug boş çıkarsa da slug\'sız adrese gider', () => {
    expect(goHedefi('42', '!!!')).toBe('/urun/42');
  });

  it('kimlik sayı değilse yönlendirmez', () => {
    expect(goHedefi('abc', 'Whey')).toBeNull();
    expect(goHedefi('12x', 'Whey')).toBeNull();
    expect(goHedefi('', 'Whey')).toBeNull();
  });
});

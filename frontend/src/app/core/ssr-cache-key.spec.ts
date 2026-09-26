import { ssrOnbellekAnahtari } from './ssr-cache-key';

describe('ssrOnbellekAnahtari', () => {
  it('sayfaların okuduğu parametrelerle ham adresi anahtar yapar', () => {
    for (const adres of [
      '/',
      '/?page=3',
      '/?brands=a&brands=b&search=whey&sort=price-asc',
      '/marka/hardline/indirim-kodu?satici=bayi&page=2',
      '/kategori/protein-tozu?urun=4304',
    ]) {
      expect(ssrOnbellekAnahtari('GET', adres)).toBe(adres);
    }
  });

  it('tanınmayan parametreli istek önbelleğe girmez (anahtardan atılmaz)', () => {
    // Atılsaydı "merge" sayfalama bağlantıları ?r=... ile temiz anahtara yazılırdı.
    expect(ssrOnbellekAnahtari('GET', '/?r=123')).toBeNull();
    expect(ssrOnbellekAnahtari('GET', '/?page=2&utm_source=x')).toBeNull();
    expect(ssrOnbellekAnahtari('GET', '/?PAGE=2')).toBeNull();
  });

  it('kişiye özel ve GET olmayan istekler önbelleğe girmez', () => {
    expect(ssrOnbellekAnahtari('POST', '/')).toBeNull();
    expect(ssrOnbellekAnahtari('GET', '/favorilerim')).toBeNull();
    expect(ssrOnbellekAnahtari('GET', '/?recover=abc')).toBeNull();
  });
});

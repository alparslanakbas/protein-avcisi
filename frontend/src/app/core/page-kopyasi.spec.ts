import { pageKopyasiHedefi } from './page-kopyasi';

describe('pageKopyasiHedefi', () => {
  it('ürün sayfasının page parametresini atıp temiz adrese yönlendirir', () => {
    expect(
      pageKopyasiHedefi(
        '/urun/676/gold-mass-gainer-cikolata-1400-g',
        '/urun/676/gold-mass-gainer-cikolata-1400-g?page=30',
      ),
    ).toBe('/urun/676/gold-mass-gainer-cikolata-1400-g');
  });

  it('inceleme sayfasında ve slug olmadan da çalışır', () => {
    expect(pageKopyasiHedefi('/urun-inceleme/12/x', '/urun-inceleme/12/x?page=2')).toBe(
      '/urun-inceleme/12/x',
    );
    expect(pageKopyasiHedefi('/urun/676', '/urun/676?page=2')).toBe('/urun/676');
  });

  it('diğer parametreleri korur', () => {
    expect(pageKopyasiHedefi('/urun/1/a', '/urun/1/a?utm_source=x&page=3')).toBe(
      '/urun/1/a?utm_source=x',
    );
  });

  it('page yoksa ya da adres ürün sayfası değilse dokunmaz', () => {
    expect(pageKopyasiHedefi('/urun/1/a', '/urun/1/a?utm_source=x')).toBeNull();
    expect(pageKopyasiHedefi('/', '/?page=3')).toBeNull();
    expect(pageKopyasiHedefi('/kategori/protein-tozu', '/kategori/protein-tozu?page=2')).toBeNull();
    expect(
      pageKopyasiHedefi('/marka/x/indirim-kodu', '/marka/x/indirim-kodu?page=2'),
    ).toBeNull();
    expect(pageKopyasiHedefi('/urun/1/a/b', '/urun/1/a/b?page=2')).toBeNull();
  });
});

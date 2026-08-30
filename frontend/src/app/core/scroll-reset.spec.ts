import { NavigationSnapshot, routePath, shouldResetScroll } from './scroll-reset';

// Bu testlerin varlık sebebi gerçek bir üretim hatası: gizlilik sayfasındaki
// "Bu sayfada" bağlantılarına ilk tıklama sayfayı en üste atıyordu, ikinci
// tıklama doğru yere götürüyordu. Sebep, yol karşılaştırmasının fragment'i
// içermesiydi. Kullanıcı buldu; bir test olsaydı yakalanırdı.

describe('routePath', () => {
  it('sorgu parametrelerini atar', () => {
    expect(routePath('/kategori/protein-tozu?sayfa=2')).toBe('/kategori/protein-tozu');
  });

  it('REGRESYON: fragment i atar', () => {
    expect(routePath('/gizlilik-politikasi#kvkk-haklari')).toBe('/gizlilik-politikasi');
  });

  it('sorgu ve fragment birlikteyken ikisini de atar', () => {
    expect(routePath('/marka/hardline/protein-tozu?sirala=ucuz#liste')).toBe('/marka/hardline/protein-tozu');
  });

  it('sade yolu olduğu gibi bırakır', () => {
    expect(routePath('/iletisim')).toBe('/iletisim');
  });
});

describe('shouldResetScroll', () => {
  // Component kimliği için referans eşitliği yeterli; gerçek sınıflara
  // ihtiyaç yok.
  const SayfaA = { ad: 'A' };
  const SayfaB = { ad: 'B' };
  const anlik = (component: unknown, path: string): NavigationSnapshot => ({ component, path });

  it('ilk gezinmede sıfırlamaz', () => {
    // Belge zaten en üstte açılır. Ayrıca adres bir fragment taşıyorsa
    // (paylaşılmış bölüm bağlantısı) tarayıcının kaydırmasını bozmamalıyız.
    expect(shouldResetScroll(null, anlik(SayfaA, '/gizlilik-politikasi'))).toBe(false);
  });

  it('REGRESYON: aynı sayfada yalnızca fragment değiştiyse sıfırlamaz', () => {
    // routePath fragment'i attığı için iki yol da aynı görünür.
    const once = anlik(SayfaA, '/gizlilik-politikasi');
    const sonra = anlik(SayfaA, routePath('/gizlilik-politikasi#kvkk-haklari'));
    expect(shouldResetScroll(once, sonra)).toBe(false);
  });

  it('gerçekten başka bir sayfaya geçilince sıfırlar', () => {
    expect(shouldResetScroll(anlik(SayfaA, '/iletisim'), anlik(SayfaB, '/rehber'))).toBe(true);
  });

  it('aynı component ama farklı yol ise sıfırlar', () => {
    // Marka sayfasındaki "Diğer Markalar" bağlantıları aynı component'te
    // kalıyor; yol karşılaştırılmasaydı kaydırma olduğu yerde kalırdı.
    expect(shouldResetScroll(anlik(SayfaA, '/marka/hiq'), anlik(SayfaA, '/marka/ssn'))).toBe(true);
  });

  it('hiçbir şey değişmediyse sıfırlamaz', () => {
    expect(shouldResetScroll(anlik(SayfaA, '/rehber'), anlik(SayfaA, '/rehber'))).toBe(false);
  });

  it('ürün modalı açılırken sıfırlamaz', () => {
    // Ana sayfada bir ürüne tıklamak '/' -> '/urun/12' gibi görünür ama
    // aynı sayfanın üstündeki bir katmandır.
    expect(shouldResetScroll(anlik(SayfaA, '/'), anlik(SayfaA, '/urun/12'))).toBe(false);
  });

  it('ürün modalı kapanırken sıfırlamaz', () => {
    expect(shouldResetScroll(anlik(SayfaA, '/urun/12'), anlik(SayfaA, '/'))).toBe(false);
  });

  it('ürün modalından gerçek bir sayfaya geçince sıfırlar', () => {
    // İstisna yalnızca aynı component içinde geçerli.
    expect(shouldResetScroll(anlik(SayfaA, '/urun/12'), anlik(SayfaB, '/marka/hiq'))).toBe(true);
  });
});

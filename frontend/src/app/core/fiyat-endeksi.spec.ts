import { ayAdi, degisimMetni, kategoriAdi, oncekiAyaGore, TUM_KATEGORILER } from './fiyat-endeksi';

describe('fiyat endeksi metinleri', () => {
  it('ay adını yazıyor', () => {
    expect(ayAdi('2026-10-01')).toBe('Ekim 2026');
    expect(ayAdi('2027-01-01')).toBe('Ocak 2027');
  });

  it('bir önceki ayı doğru ekle veriyor, yıl dönümünde Aralık', () => {
    expect(oncekiAyaGore('2026-10-01')).toBe("Eylül'e");
    expect(oncekiAyaGore('2026-12-01')).toBe("Kasım'a");
    expect(oncekiAyaGore('2027-01-01')).toBe("Aralık'a");
  });

  it('değişimi işaretli ve Türkçe yüzdeyle yazıyor', () => {
    expect(degisimMetni(1.41)).toBe('+%1,41');
    expect(degisimMetni(-0.01)).toBe('−%0,01');
    expect(degisimMetni(0)).toBe('%0,00');
  });

  it('kategori adını ve bütün kategoriler satırını adlandırıyor', () => {
    expect(kategoriAdi('protein-tozu')).toBe('Protein Tozu');
    expect(kategoriAdi(TUM_KATEGORILER)).toBe('Bütün kategoriler');
  });
});

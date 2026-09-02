import { describe, expect, it } from 'vitest';

import { normalizeSearchText } from './search-normalize';

// Bu testlerin varlık sebebi somut bir üretim hatası: markalar dizininde
// "hiq" yazınca 0 sonuç çıkıyor, "HIQ" yazınca çıkıyordu. Sebep
// `toLocaleLowerCase('tr-TR')`'nin "I" -> "ı" dönüşümüydü.
describe('normalizeSearchText', () => {
  // Marka adı ile kullanıcının yazdığı metin AYNI değere inmeli — asıl
  // kural bu; testlerin geri kalanı bunun örnekleri.
  it.each([
    ['HIQ', 'hiq'],
    ['HIQ', 'Hiq'],
    ['HIQ', 'hIq'],
    ['Imperium Supplements', 'imperium supplements'],
    ['Fit Çarşı', 'fit carsi'],
    ['Fit Çarşı', 'fit çarşı'],
    ['Yeşilmarka', 'yesilmarka'],
    ['BigJoy', 'bigjoy'],
    ['Vitabear', 'VITABEAR'],
  ])('"%s" ile "%s" aynı değere iner', (marka, aranan) => {
    expect(normalizeSearchText(marka)).toBe(normalizeSearchText(aranan));
  });

  // Hatanın kendisi: tr-TR küçültmesi kullanılsaydı bu eşleşme kırılırdı.
  it('büyük ASCII I noktasız ı üretmez', () => {
    expect(normalizeSearchText('HIQ')).toBe('hiq');
    expect(normalizeSearchText('Imperium')).toBe('imperium');
  });

  // Türkçe noktalı İ, invariant küçültmede olduğu gibi kalıyordu.
  it('Türkçe noktalı İ küçültülüyor', () => {
    expect(normalizeSearchText('İÇECEK')).toBe('icecek');
    expect(normalizeSearchText('VİTAMİN')).toBe('vitamin');
  });

  it('Türkçe harfler ASCII karşılığına iniyor', () => {
    expect(normalizeSearchText('ÇĞIİÖŞÜ')).toBe('cgiiosu');
    expect(normalizeSearchText('çğıiöşü')).toBe('cgiiosu');
  });

  // Aranan metin marka adı + kategori etiketlerinden oluşuyor ve 80
  // karakteri aşabiliyor; slugify bu yüzden kullanılamadı.
  it('uzun metni kırpmıyor', () => {
    const uzun = `Hardline ${'protein tozu kreatin amino asitler vitamin '.repeat(4)}`;

    const sonuc = normalizeSearchText(uzun);

    expect(sonuc.length).toBeGreaterThan(80);
    expect(sonuc.endsWith('vitamin')).toBe(true);
  });

  it('ardışık boşlukları teke indiriyor ve uçları kırpıyor', () => {
    expect(normalizeSearchText('  Torq   Nutrition  ')).toBe('torq nutrition');
  });

  it('kısmi yazımda alt dize eşleşmesi korunuyor', () => {
    expect(normalizeSearchText('Hardline').includes(normalizeSearchText('hard'))).toBe(true);
    expect(normalizeSearchText('Fit Çarşı').includes(normalizeSearchText('cars'))).toBe(true);
  });
});

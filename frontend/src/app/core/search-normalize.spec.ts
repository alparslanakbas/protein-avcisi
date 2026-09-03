import { describe, expect, it } from 'vitest';

import { matchesSearch, normalizeSearchText } from './search-normalize';

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

// Kullanıcı bildirdi: "protein ocean yazıyorum sonuç yok diyor, çünkü marka
// adı ProteinOcean. Diğer markaları buluyor."
describe('matchesSearch', () => {
  it('boşluklu yazımla bitişik marka adını bulur', () => {
    expect(matchesSearch('ProteinOcean', 'protein ocean')).toBe(true);
    expect(matchesSearch('ProteinOcean', 'Protein Ocean')).toBe(true);
    expect(matchesSearch('ProteinOcean', 'proteinocean')).toBe(true);
  });

  it('boşluksuz yazımla ayrık marka adını bulur', () => {
    expect(matchesSearch('Swiss Nutrition', 'swissnutrition')).toBe(true);
    expect(matchesSearch('Prime Nutrition', 'primenutrition')).toBe(true);
  });

  it('kelime sırası önemsiz', () => {
    expect(matchesSearch('Swiss Nutrition', 'nutrition swiss')).toBe(true);
  });

  it('kısmi yazımda da bulur', () => {
    expect(matchesSearch('ProteinOcean', 'ocean')).toBe(true);
    expect(matchesSearch('Muscle Pump', 'pump')).toBe(true);
  });

  // Düzeltilen Türkçe tuzağı burada da geçerli olmalı.
  it('küçük/büyük harf ve Türkçe harf farkı engel değil', () => {
    expect(matchesSearch('HIQ', 'hiq')).toBe(true);
    expect(matchesSearch('Imperium Supplements', 'imperium')).toBe(true);
    expect(matchesSearch('Fit Çarşı', 'fit carsi')).toBe(true);
    expect(matchesSearch('Yeşilmarka', 'yesil')).toBe(true);
  });

  it('boş arama her şeyi geçirir', () => {
    expect(matchesSearch('BigJoy', '')).toBe(true);
    expect(matchesSearch('BigJoy', '   ')).toBe(true);
  });

  // ALAKASIZ SONUÇ ÜRETMEMELİ: kelime kelime eşleşme gevşetiyor, ama
  // kelimelerin HEPSİ geçmek zorunda.
  it('eşleşmeyeni bulmaz', () => {
    expect(matchesSearch('ProteinOcean', 'protein ocean kreatin')).toBe(false);
    expect(matchesSearch('BigJoy', 'hardline')).toBe(false);
    expect(matchesSearch('Swiss Nutrition', 'swiss xyz')).toBe(false);
  });
});

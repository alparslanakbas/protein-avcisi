import { describe, expect, it } from 'vitest';
import { buildProductDescription } from './meta-description';

// Örnek metinler gerçek üretim verisinden alındı — üç markanın da kendine
// özgü bir baş kalıbı var ve regex'ler bu kalıplara göre yazıldı.
function build(description: string | null, overrides: Partial<Parameters<typeof buildProductDescription>[0]> = {}) {
  return buildProductDescription({
    displayName: 'Test Ürünü',
    brandName: 'TestMarka',
    priceText: '100,00 TL',
    discountPercent: 0,
    description,
    ...overrides,
  });
}

describe('buildProductDescription', () => {
  it('açıklama yoksa fiyat şablonuna düşer', () => {
    const result = build(null);
    expect(result).toContain('Test Ürünü güncel fiyatı 100,00 TL');
    expect(result).toContain('TestMarka');
  });

  it('indirimli üründe indirim oranını yazar', () => {
    const result = build(null, { discountPercent: 25 });
    expect(result).toContain('%25 doğrulanmış indirim');
  });

  it('"Açıklama:" önekini atar', () => {
    const result = build('Açıklama: HIQ DUALFORCE, antrenman öncesi tüketim için geliştirilmiş toz formda bir spor gıdasıdır.');
    expect(result.startsWith('HIQ DUALFORCE')).toBe(true);
    expect(result).not.toContain('Açıklama:');
  });

  // Türkçe "İ" büyük harfi, JavaScript'in büyük/küçük harf duyarsız
  // eşleşmesinde "i" ile EŞLEŞMEZ — bu yüzden kalıplar açık harf sınıflarıyla
  // yazıldı. Bu test o davranışı kilitliyor.
  it('Türkçe İ içeren "NEDİR ?:" başlığını atar', () => {
    const result = build('CREATINE CREAPURE® NEDİR ?: Alman üretici ALZCHEM firmasının patentiyle üretilmiş saf kreatindir.');
    expect(result.startsWith('Alman üretici')).toBe(true);
    expect(result).not.toContain('NEDİR');
  });

  it('Türkçe İ ile başlayan "İçerik:" önekini atar', () => {
    const result = build('İçerik: Kafein; kahve, çay gibi gıdalarda doğal olarak bulunan bir maddedir.');
    expect(result.startsWith('Kafein;')).toBe(true);
    expect(result).not.toContain('İçerik:');
  });

  it('metin ürün adıyla başlıyorsa tekrarı atar', () => {
    const result = build('Test Ürünü GMP ve HACCP sertifikasına sahip tesislerde üretilen saf bir proteindir.');
    expect(result.startsWith('GMP ve HACCP')).toBe(true);
  });

  // "GI+ ürünü; lif..." metninden adı atmak "ürünü; lif..." bırakıyordu.
  it('ad tekrarını atmak cümleyi ortasından kesiyorsa vazgeçer', () => {
    const result = build('Test Ürünü; lif ve prebiyotik bileşenleri tek üründe birleştiren pratik bir içecek tozudur.');
    expect(result.startsWith('Test Ürünü;')).toBe(true);
  });

  it('sert boşluk karakterlerini normal boşluğa çevirir', () => {
    const result = build('Bu ürün yoğun antrenman yapan sporcular için geliştirilmiştir.');
    expect(result).not.toContain(' ');
    expect(result).toContain('Bu ürün yoğun antrenman');
  });

  it('yalnızca ilk cümleyi alır', () => {
    const result = build('Yoğun antrenman yapan sporcular için geliştirilmiş bir üründür. İkinci cümle buraya girmemeli.');
    expect(result).not.toContain('İkinci cümle');
  });

  it('çok kısa açıklamayı kullanmaz, şablona döner', () => {
    const result = build('Protein tozu.');
    expect(result).toContain('güncel fiyatı');
  });

  it('uzun tek cümleyi kırpar ve arama sonucu sınırında kalır', () => {
    const long = 'Bu ürün ' + 'çok uzun bir açıklama metni içermektedir '.repeat(10) + 've burada biter.';
    const result = build(long);
    expect(result).toContain('…');
    expect(result.length).toBeLessThanOrEqual(165);
  });

  it('fiyat bilgisi her durumda korunur', () => {
    const result = build('Yoğun antrenman yapan sporcular için geliştirilmiş bir spor gıdasıdır.');
    expect(result).toContain('100,00 TL');
  });
});

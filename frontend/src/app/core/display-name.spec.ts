import { displayName } from './display-name';

describe('displayName', () => {
  it('ALL CAPS ürün isimlerini Title Case yapar', () => {
    expect(displayName('WHEY ISOLATE')).toBe('Whey Isolate');
    expect(displayName('TANIŞMA PAKETİ 1')).toBe('Tanışma Paketi 1');
  });

  it('bilinen kısaltmaları büyük harfte korur', () => {
    expect(displayName('HIQ CREA500 Creatine 240g')).toBe('HIQ Crea500 Creatine 240g');
    expect(displayName('SSN BCAA 315 GR')).toBe('SSN BCAA 315 GR');
  });

  it('zaten küçük harfli birim yazımlarına dokunmaz', () => {
    expect(displayName('HIQ Creatine 360g')).toBe('HIQ Creatine 360g');
  });

  it('parantez ve rakamları bozmaz', () => {
    expect(displayName('NOX2 540 GR (36 GR*15 ADET)')).toBe('NOX2 540 GR (36 GR*15 Adet)');
  });

  it('Türkçe İ/ı harflerini doğru büyütür/küçültür', () => {
    expect(displayName('KREATİN MONOHİDRAT')).toBe('Kreatin Monohidrat');
    expect(displayName('IŞIL VİTAMİN')).toBe('Işıl Vitamin');
  });

  it('bağlaçları küçük bırakır', () => {
    expect(displayName('CREATINE VE BCAA PAKETİ')).toBe('Creatine ve BCAA Paketi');
  });

  it('zaten Title Case olan isimlerde değişiklik yapmaz (idempotent)', () => {
    const already = 'HIQ Creatine 360g';
    expect(displayName(already)).toBe(already);
  });

  it('boş string ile çağrılırsa hata vermez', () => {
    expect(displayName('')).toBe('');
  });
});

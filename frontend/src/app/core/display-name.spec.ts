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

  // --- Kültür kuralı regresyonları ---
  // Bu testler canlıda ölçülen gerçek bozulmalardan çıktı: 1748 sayfa
  // tarandığında ~200'ünde İngilizce kelimeler noktasız "ı" ile
  // yazılmıştı ("Proteın", "Bıgwhey"). Sebep tr-TR'nin varsayılan
  // olmasıydı; varsayılan ters çevrildi.
  it('İngilizce kelimelerdeki I harfini noktalı i yapar', () => {
    expect(displayName('PROTEIN BAR')).toBe('Protein Bar');
    expect(displayName('BIGJOY BIGWHEY GOLD')).toBe('Bigjoy Bigwhey Gold');
    expect(displayName('HYALURONIC ACID')).toBe('Hyaluronic Acid');
    expect(displayName('MAGNESIUM BISGLYCINATE')).toBe('Magnesium Bisglycinate');
    expect(displayName('DAILY MULTIVITAMIN')).toBe('Daily Multivitamin');
  });

  it('Türkçe harf taşıyan kelimelerde tr-TR kuralında kalır', () => {
    expect(displayName('TANIŞMA PAKETİ')).toBe('Tanışma Paketi');
    expect(displayName('SAĞLIKLI ATIŞTIRMALIK')).toBe('Sağlıklı Atıştırmalık');
  });

  it('Türkçe harf taşımayan Türkçe kelimeleri istisna listesiyle korur', () => {
    // Bunların tek Türkçe işareti sondaki noktasız ı — harften anlaşılmıyor,
    // liste olmasa "Fistik", "Aromali" olurlardı.
    expect(displayName('FISTIK KREMASI')).toBe('Fıstık Kreması');
    expect(displayName('FINDIK AROMALI')).toBe('Fındık Aromalı');
    expect(displayName('23.YIL YAPILANMASI')).toBe('23.Yıl Yapılanması');
    expect(displayName('SARIMSAK')).toBe('Sarımsak');
  });

  it('markanın noktalı İ yerine I yazdığı Türkçe kelimeleri de düzeltir', () => {
    // Kaynak veride "EKONOMİK" değil "EKONOMIK" yazıyor; eski kural
    // bunu "Ekonomık" yapıyordu.
    expect(displayName('EKONOMIK PAKET')).toBe('Ekonomik Paket');
    expect(displayName('MIKRONIZE KREATIN')).toBe('Mikronize Kreatin');
  });

});

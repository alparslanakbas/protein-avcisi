import { describe, expect, it } from 'vitest';
import { buildPageTitle, buildProductDescription, buildReviewDescription, clampTitle, formatPercent } from './meta-description';

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

  // Canlıdan alınan gerçek örnek (1 Eylül, /urun/140). Markanın metni ürün
  // adını arka arkaya iki kez yazıyor; ad kontrolü metindeki yazım farkı
  // ("2100 g (Creme Caramel)" ile "2100gr Creme Caramel") yüzünden tutmuyordu.
  it('metin ürün adını iki kez yazdıysa birinci kopyayı atar', () => {
    const result = build(
      'SSN Sports Style Nutrition Command Quadro Whey 2100 g (Creme Caramel) ' +
        'SSN Sports Style Nutrition Command Quadro Whey dört farklı protein ' +
        'kaynağını bir arada sunan bir üründür.',
      { displayName: 'SSN Sports Style Nutrition Command Quadro Whey 2100gr Creme Caramel' },
    );
    expect(result).not.toContain('(Creme Caramel) SSN');
    expect(result.indexOf('Command Quadro Whey')).toBe(result.lastIndexOf('Command Quadro Whey'));
  });
});

describe('clampTitle', () => {
  it('sınırın altındaki başlığa dokunmaz', () => {
    const t = 'HIQ Kreatin Fiyatları 2026 | Protein Avcısı';
    expect(clampTitle(t)).toBe(t);
  });

  // Canlıdan gerçek örnek: 160 sayfalık örneğin %16'sı böyleydi ve neredeyse
  // tamamı marka×kategori sayfasıydı — sayfa başına en çok gösterim alan tip.
  it('kuyruk sığmıyorsa yarım bırakmak yerine tamamını atar', () => {
    const t = 'ProteinOcean Kreatin Fiyatları ve İndirimleri 2026 | Protein Avcısı';
    const sonuc = clampTitle(t);
    expect(sonuc).toBe('ProteinOcean Kreatin Fiyatları ve İndirimleri 2026');
    expect(sonuc).not.toContain('|');
    expect(sonuc).not.toContain('…');
  });

  it('hiçbir başlık ayıraç ya da üç noktayla sarkık bitmez', () => {
    const ornekler = [
      'West Nutrition Amino Asitler Fiyatları ve İndirimleri 2026 | Protein Avcısı',
      'SSN L-Carnitine & CLA Fiyatları ve İndirimleri 2026 | Protein Avcısı',
      'Swiss Nutrition Protein Tozu Fiyatları ve İndirimleri 2026 | Protein Avcısı',
    ];
    for (const t of ornekler) {
      expect(clampTitle(t)).not.toMatch(/[|·,:;&/–-]\s*…?\s*$/);
    }
  });

  it('kuyruksuz başlık da uzunsa kelime sınırından kırpar', () => {
    const t = 'A'.repeat(30) + ' ' + 'B'.repeat(30) + ' ' + 'C'.repeat(30);
    const sonuc = clampTitle(t);
    expect(sonuc.endsWith('…')).toBe(true);
    expect(sonuc.length).toBeLessThanOrEqual(66);
  });
});

describe('buildPageTitle', () => {
  it('her şey sığıyorsa marka kuyruğunu korur', () => {
    expect(buildPageTitle('HIQ Crea500', 'Fiyatı ve Fiyat Geçmişi', 'HIQ')).toBe(
      'HIQ Crea500 Fiyatı ve Fiyat Geçmişi | HIQ',
    );
  });

  // Canlıdan gerçek örnekler: sayı biriminden koparak yetim kalıyordu. (İlk
  // örnek SSN'in Quadro Whey'iydi; açılım kuralından beri o ad kırpılmıyor.)
  it('sonda yetim kalan sayıyı bırakmaz', () => {
    const sonuc = buildPageTitle(
      'Muscle Station Fit Snack Protein Bar Coconut 40 Gr 1 Adet',
      'İncelemesi',
      'Musclestation',
    );
    expect(sonuc).toBe('Muscle Station Fit Snack Protein Bar Coconut 40 Gr… İncelemesi');
  });

  // 6 Ekim: uzun ek ada 41 karakter bırakıyordu; aroması farklı iki ürün
  // "Big Joy Big Mass +GH Factors… Fiyatı ve Fiyat Geçmişi" başlığını paylaşıyordu.
  it('ad uzun ekle sığmıyorsa kısa eke geçer, varyant kuyruğu kalır', () => {
    const cilek = buildPageTitle('Big Joy Big Mass +GH Factors 5000 Gr (Çilek)', 'Fiyatı ve Fiyat Geçmişi', 'BigJoy', 'Fiyatı');
    const cikolata = buildPageTitle('Big Joy Big Mass +GH Factors 5000 Gr (Çikolata)', 'Fiyatı ve Fiyat Geçmişi', 'BigJoy', 'Fiyatı');
    expect(cilek).toBe('Big Joy Big Mass +GH Factors 5000 Gr (Çilek) Fiyatı');
    expect(cikolata).toBe('Big Joy Big Mass +GH Factors 5000 Gr (Çikolata) Fiyatı');
  });

  it('kısa ek de sığmazsa adı kısa ekle kırpar', () => {
    const sonuc = buildPageTitle(
      'Trio Move Protein Bar 9 Adet X 50 Gr Sütlü Çikolatalı & Matcha Protein Bar',
      'Fiyatı ve Fiyat Geçmişi',
      'Trio',
      'Fiyatı',
    );
    expect(sonuc).toBe('Trio Move Protein Bar 9 Adet X 50 Gr Sütlü Çikolatalı… Fiyatı');
  });

  it('kısaltma markasının açılımını yalnız başlık sığmadığında atar', () => {
    expect(
      buildPageTitle('SSN Sports Style Nutrition Command Quadro Whey 366 gr Çikolata', 'Fiyatı ve Fiyat Geçmişi', 'SSN', 'Fiyatı'),
    ).toBe('SSN Command Quadro Whey 366 gr Çikolata Fiyatı ve Fiyat Geçmişi');
    expect(buildPageTitle('SSN Sports Style Nutrition Bcaa', 'İncelemesi', 'SSN')).toBe(
      'SSN Sports Style Nutrition Bcaa İncelemesi | SSN',
    );
  });

  it('açılım olmayan kelimeleri atmaz', () => {
    // "Pro Performance 100%" GNC'nin açılımı değil.
    expect(
      buildPageTitle('GNC Pro Performance 100% Whey Protein 2268 gr Çikolata', 'Fiyatı ve Fiyat Geçmişi', 'GNC', 'Fiyatı'),
    ).toBe('GNC Pro Performance 100% Whey Protein 2268 gr Çikolata Fiyatı');
  });

  it('sonda yetim kalan tek harfi bırakmaz', () => {
    const sonuc = buildPageTitle(
      'Bigjoy Classic High Protein Bar 45g x 16 Adet',
      'Fiyatı ve Fiyat Geçmişi',
      'BigJoy',
    );
    expect(sonuc).not.toMatch(/\sx…/);
    expect(sonuc).toContain('45g');
  });

  it('birimiyle tam olan parçayı korur', () => {
    const sonuc = buildPageTitle(
      'Argitorq L-Arginine Capsule 1250 120 Kapsül 60 Servis',
      'Fiyatı ve Fiyat Geçmişi',
      'Torq Nutrition',
    );
    expect(sonuc).not.toMatch(/\s\d+…/);
  });
});

describe('buildReviewDescription', () => {
  const temel = {
    displayName: 'Hardline Whey 3 Matrix 2300 Gr',
    priceText: '1.899,00 TL',
    discountPercent: 0,
    gecmisGunSayisi: 30,
  };

  it('fiyatı öne alıyor — eskiden 247 sayfada aynı cümle vardı ve tek sayı yoktu', () => {
    const d = buildReviewDescription(temel);
    expect(d).toContain('1.899,00 TL');
    expect(d.indexOf('1.899,00 TL')).toBeLessThan(d.indexOf('bağımsız inceleme'));
  });

  it('yeterli geçmiş varsa gün sayısını anıyor', () => {
    expect(buildReviewDescription({ ...temel, gecmisGunSayisi: 30 })).toContain('30 günlük fiyat geçmişi');
  });

  // Asıl koruma: veri inceyken iddia BÜYÜTÜLMÜYOR. Katalogda 4825 ürünün
  // yalnızca 515'inde 14+ gün veri var.
  it('geçmiş inceyse gün sayısını ANMIYOR', () => {
    const d = buildReviewDescription({ ...temel, gecmisGunSayisi: 2 });
    expect(d).not.toContain('günlük fiyat geçmişi');
    expect(d).toContain('1.899,00 TL');
  });

  it('indirim + yeterli geçmiş varsa indirimi öne alıyor', () => {
    const d = buildReviewDescription({ ...temel, discountPercent: 18, gecmisGunSayisi: 20 });
    expect(d).toContain('%18 doğrulanmış indirim');
    expect(d).toContain('markanın etiketine değil');
  });

  // En önemli test: iki günlük veriye "doğrulanmış indirim" demiyoruz.
  // Bu sitenin varlık sebebi markaların dayanaksız indirim iddiasını
  // teşhir etmek; aynısını yapmak markayı içeriden çürütürdü.
  it('geçmiş inceyken indirim İDDİA ETMİYOR', () => {
    const d = buildReviewDescription({ ...temel, discountPercent: 40, gecmisGunSayisi: 2 });
    expect(d).not.toContain('indirim');
    expect(d).toContain('güncel fiyatı');
  });

  it('Google sınırını aşmıyor', () => {
    const d = buildReviewDescription({
      ...temel,
      displayName: 'Çok Uzun Bir Ürün Adı '.repeat(8),
      discountPercent: 25,
      gecmisGunSayisi: 30,
    });
    expect(d.length).toBeLessThanOrEqual(155);
  });
});

describe('indirim yüzdesi biçimi', () => {
  // Canlıda "%30.8 doğrulanmış indirim" çıkıyordu: nokta ayraçlı ve bir SERP
  // parçacığı için gereksiz hassasiyette.
  it('inceleme açıklamasında tam sayı', () => {
    const d = buildReviewDescription({
      displayName: 'X', priceText: '10,00 TL', discountPercent: 30.8, gecmisGunSayisi: 28,
    });
    expect(d).toContain('%31 doğrulanmış indirim');
    expect(d).not.toContain('30.8');
  });

  it('ürün açıklamasında da tam sayı', () => {
    const d = buildProductDescription({
      displayName: 'X', brandName: 'Y', priceText: '10,00 TL', discountPercent: 7.2, description: null,
    });
    expect(d).toContain('%7 doğrulanmış indirim');
    expect(d).not.toContain('7.2');
  });
});

describe('clampTitle — marka kuyrukta olmalı', () => {
  // GERÇEK OLAY (5 Eylül): ana sayfanın başlığı "Protein Avcısı | Güncel
  // İndirim ve Kampanyalar — Spor Takviyesi Fiyat Takibi" idi. Marka BAŞTA,
  // uzunluk 76. clampTitle son " | " işaretinden sonrasını attığı için
  // canlıda geriye sadece "Protein Avcısı" kalıyordu — sitenin en önemli
  // sayfasında sıfır anahtar kelime, ve bu hiçbir yerde hata vermiyordu.
  it('marka BAŞTAYSA içerik tamamen kaybolur — bu yüzden marka kuyrukta yazılmalı', () => {
    const markaBasta = 'Protein Avcısı | Güncel İndirim ve Kampanyalar — Spor Takviyesi Fiyat Takibi';
    expect(markaBasta.length).toBeGreaterThan(65);
    expect(clampTitle(markaBasta)).toBe('Protein Avcısı');
  });

  it('marka kuyruktaysa ve sığıyorsa başlığa hiç dokunmuyor', () => {
    const dogru = 'Gerçek Protein ve Takviye İndirimleri | Protein Avcısı';
    expect(dogru.length).toBeLessThanOrEqual(60);
    expect(clampTitle(dogru)).toBe(dogru);
  });
});

describe('formatPercent', () => {
  // Ana sayfanın öne çıkan fırsatı "%44.4" gösteriyordu (7 Ekim): ham değer nokta ayraçla basılıyordu.
  it('Türkçe ondalık virgül ve en fazla bir hane', () => {
    expect(formatPercent(44.4)).toBe('44,4');
    expect(formatPercent(15)).toBe('15');
    expect(formatPercent(12.04)).toBe('12');
  });
});

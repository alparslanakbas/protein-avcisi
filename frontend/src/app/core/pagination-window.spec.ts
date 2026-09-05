import { describe, expect, it } from 'vitest';

import { clampTitle } from './meta-description';
import { adrestenSayfa, sayfaliBaslik, sayfaPenceresi } from './pagination-window';

// Bu testlerin varlık sebebi bir SEO ölçümü: sayfalama yalnızca tıklama
// olayıyla çalıştığı için sunucudan gelen HTML'de hiç bağlantı yoktu ve
// katalogun büyük kısmı Google için erişilemezdi. Aşağıdaki kurallar o
// bağlantıların hangi sayfalara verileceğini belirliyor.
describe('sayfaPenceresi', () => {
  it('tek sayfada yalnızca o sayfa var', () => {
    expect(sayfaPenceresi(1, 1)).toEqual([1]);
  });

  it('sayfa yoksa boş dönüyor', () => {
    expect(sayfaPenceresi(1, 0)).toEqual([]);
  });

  it('kısa listede boşluk işareti çıkmıyor', () => {
    expect(sayfaPenceresi(1, 5)).toEqual([1, 2, 3, 4, 5]);
  });

  // Asıl kural: ilk ve son sayfa HER ZAMAN bağlantı alıyor. Tarayıcının
  // 205 sayfalık bir zinciri tek tek yürümesi beklenemez; iki sabit uç
  // derinliği kısaltıyor.
  it('uzun listede ilk ve son sayfa her zaman var', () => {
    const pencere = sayfaPenceresi(100, 205);
    expect(pencere[0]).toBe(1);
    expect(pencere[pencere.length - 1]).toBe(205);
  });

  it('ortadaki sayfada iki yana da boşluk işareti giriyor', () => {
    expect(sayfaPenceresi(100, 205)).toEqual([1, null, 98, 99, 100, 101, 102, null, 205]);
  });

  it('başta yalnızca sağ tarafta boşluk oluyor', () => {
    expect(sayfaPenceresi(2, 50)).toEqual([1, 2, 3, 4, null, 50]);
  });

  it('sonda yalnızca sol tarafta boşluk oluyor', () => {
    expect(sayfaPenceresi(49, 50)).toEqual([1, null, 47, 48, 49, 50]);
  });

  // Aynı sayfa iki kez bağlantı almamalı: yinelenen numara hem çirkin
  // görünür hem de aynı adrese iki iç bağlantı demek.
  it('numaralar benzersiz ve artan sırada', () => {
    const numaralar = sayfaPenceresi(3, 40).filter((x): x is number => x !== null);
    expect(new Set(numaralar).size).toBe(numaralar.length);
    expect([...numaralar].sort((a, b) => a - b)).toEqual(numaralar);
  });

  // Sayfa numarası adresten geliyor, yani ziyaretçi elle bozuk değer
  // yazabilir. Pencere yine de geçerli aralıkta kalmalı.
  it('aralık dışındaki mevcut sayfa sınıra çekiliyor', () => {
    expect(sayfaPenceresi(999, 5)).toEqual([1, 2, 3, 4, 5]);
    expect(sayfaPenceresi(-4, 5)).toEqual([1, 2, 3, 4, 5]);
  });
});

describe('adrestenSayfa', () => {
  it.each([
    ['3', 3],
    ['1', 1],
    [null, 1],
    ['', 1],
    ['abc', 1],
    ['0', 1],
    ['-2', 1],
    ['2.7', 2],
  ])('"%s" -> %i', (ham, beklenen) => {
    expect(adrestenSayfa(ham)).toBe(beklenen);
  });
});

describe('sayfaliBaslik', () => {
  it('ilk sayfada başlık değişmiyor', () => {
    expect(sayfaliBaslik('Kreatin Fiyatları | ProteinAvcısı', 1)).toBe('Kreatin Fiyatları | ProteinAvcısı');
  });

  it('ek marka kuyruğundan ÖNCE giriyor', () => {
    expect(sayfaliBaslik('Kreatin Fiyatları | ProteinAvcısı', 4)).toBe('Kreatin Fiyatları – Sayfa 4 | ProteinAvcısı');
  });

  it('ayıraç yoksa sona ekleniyor', () => {
    expect(sayfaliBaslik('Kreatin Fiyatları', 4)).toBe('Kreatin Fiyatları – Sayfa 4');
  });

  // ASIL KURAL: clampTitle sığmayan başlıkta son " | " ayıracından
  // sonrasını atıyor. Sayfa eki sona konsaydı tam da kırpılan parçada
  // kalır ve uzun başlıklı sayfalar yine aynı başlığı taşırdı.
  it('kırpılan uzun başlıkta sayfa numarası hayatta kalıyor', () => {
    const uzun = 'Optimum Nutrition Sağlıklı Atıştırmalıklar Fiyatları ve İndirimleri 2026 | ProteinAvcısı';
    const kirpilmis = clampTitle(sayfaliBaslik(uzun, 7));
    expect(kirpilmis).toContain('Sayfa 7');
  });

  it('kısa başlıkta marka eki de duruyor', () => {
    const kisa = 'Kreatin Fiyatları 2026 | ProteinAvcısı';
    const kirpilmis = clampTitle(sayfaliBaslik(kisa, 2));
    expect(kirpilmis).toBe('Kreatin Fiyatları 2026 – Sayfa 2 | ProteinAvcısı');
  });
});

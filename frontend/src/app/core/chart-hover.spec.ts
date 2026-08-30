import { dedupeSameDaySamePrice, hoverAlign, nearestPointIndex, tooltipDateLabel } from './chart-hover';

// Bu hesaplar ürün modalında ÜÇ ayrı üretim hatası vermişti: aynı gün tekrar
// eden eksen etiketleri, kenardaki tooltip'in kırpılması ve aynı gün/aynı
// fiyat noktalarının hover'da tekrar tekrar görünmesi. İnceleme sayfasına da
// grafik eklenirken mantık kopyalanmadı, buraya taşındı — testler de bileşen
// yerine burada duruyor: saf fonksiyon oldukları için TestBed gerekmiyor.
describe('chart-hover', () => {
  describe('dedupeSameDaySamePrice', () => {
    it('aynı gün + aynı fiyatlı ardışık noktaları tek noktaya indirir', () => {
      const points = [
        { price: 339.15, scrapedAt: '2026-08-10T08:00:00Z' },
        { price: 339.15, scrapedAt: '2026-08-10T14:00:00Z' },
        { price: 399.0, scrapedAt: '2026-08-11T08:00:00Z' },
        { price: 399.0, scrapedAt: '2026-08-12T08:00:00Z' },
      ];

      const result = dedupeSameDaySamePrice(points);

      expect(result).toEqual([
        { price: 339.15, scrapedAt: '2026-08-10T14:00:00Z' },
        { price: 399.0, scrapedAt: '2026-08-11T08:00:00Z' },
        { price: 399.0, scrapedAt: '2026-08-12T08:00:00Z' },
      ]);
    });

    it('aynı gün ama farklı fiyatlı noktaları koruyor (gerçek bir gün-içi değişiklik)', () => {
      const points = [
        { price: 100, scrapedAt: '2026-08-10T08:00:00Z' },
        { price: 90, scrapedAt: '2026-08-10T14:00:00Z' },
      ];

      const result = dedupeSameDaySamePrice(points);

      expect(result.length).toBe(2);
    });
  });

  describe('tooltipDateLabel', () => {
    it('aynı gün tek nokta varsa sadece tarih gösterir (saat yok)', () => {
      const points = [{ price: 100, scrapedAt: '2026-08-11T08:00:00Z' }];

      const label = tooltipDateLabel(points, 0);

      expect(label).not.toMatch(/\d{2}:\d{2}$/);
    });

    it('aynı gün birden fazla nokta varsa hangi an değiştiği belli olsun diye saat ekler', () => {
      const points = [
        { price: 100, scrapedAt: '2026-08-11T08:00:00Z' },
        { price: 90, scrapedAt: '2026-08-11T20:00:00Z' },
      ];

      const label = tooltipDateLabel(points, 1);

      expect(label).toMatch(/\d{2}:\d{2}$/);
    });
  });

  describe('hoverAlign', () => {
    // Kenara yakınken ortalı hizalama tooltip'in yarısını konteynerin dışına
    // taşırıp kırpılmasına yol açıyordu (mobilde belirgindi).
    it('sol kenara yakın noktayı sola yaslar', () => {
      expect(hoverAlign(10, 600)).toBe('left');
    });

    it('sağ kenara yakın noktayı sağa yaslar', () => {
      expect(hoverAlign(590, 600)).toBe('right');
    });

    it('ortadaki noktayı ortalar', () => {
      expect(hoverAlign(300, 600)).toBe('center');
    });
  });

  describe('nearestPointIndex', () => {
    // SVG preserveAspectRatio="none" ile esniyor: ekran genişliği viewBox
    // genişliğinden farklı olabilir, ölçekleme yapılmazsa imleç yanlış
    // noktaya yapışır.
    const fakeSvg = (left: number, width: number) =>
      ({ getBoundingClientRect: () => ({ left, width }) }) as unknown as SVGSVGElement;

    const coords: [number, number][] = [
      [0, 10],
      [300, 20],
      [600, 30],
    ];

    it('ekran genişliği viewBox genişliğinden farklıyken doğru ölçekler', () => {
      // 300px genişliğinde çizilmiş 600 birimlik viewBox: ekranda 150px,
      // viewBox'ta 300 birime denk gelir — yani ortadaki nokta.
      expect(nearestPointIndex(fakeSvg(0, 300), 150, coords, 600)).toBe(1);
    });

    it('konteynerin sol ofsetini hesaba katar', () => {
      expect(nearestPointIndex(fakeSvg(100, 600), 105, coords, 600)).toBe(0);
    });

    it('nokta yoksa null döner', () => {
      expect(nearestPointIndex(fakeSvg(0, 600), 100, [], 600)).toBeNull();
    });

    it('henüz ölçülmemiş (genişliği 0) SVG için null döner, sıfıra bölmez', () => {
      expect(nearestPointIndex(fakeSvg(0, 0), 100, coords, 600)).toBeNull();
    });
  });
});

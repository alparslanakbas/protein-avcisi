import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { ProductModal } from './product-modal';

// Bu bileşenin grafik yardımcı metotları bu oturumda üç kez gerçek prod
// bug'ı vermişti — regresyon testleri bilinçli olarak buraya odaklandı.
// Hover/dedupe/tooltip hesapları inceleme sayfasıyla paylaşıldığı için
// core/chart-hover.ts'e taşındı; testleri de core/chart-hover.spec.ts'te.
// Buradaki metotlar `points` parametresi alıp saf hesaplama
// yaptığı için `deal` input'unu hiç set etmeden (ve detectChanges hiç
// çağırmadan, constructor'daki effect()'lerin tetiklenmesini önleyerek)
// doğrudan çağrılabiliyor — HTTP/route mock'una gerek yok.
describe('ProductModal - fiyat grafiği yardımcı fonksiyonları', () => {
  let component: any;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ProductModal],
      providers: [
        provideHttpClient(),
        // Şablondaki RouterLink (marka/ürün linkleri) view oluşturulurken
        // ActivatedRoute'a ihtiyaç duyuyor, detectChanges() hiç çağrılmasa bile.
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({})) } },
      ],
    });
    component = TestBed.createComponent(ProductModal).componentInstance;
  });

  describe('buildXAxisLabels', () => {
    it('boş veri için boş dizi döner', () => {
      expect(component.buildXAxisLabels([])).toEqual([]);
    });

    it('kısa veri aralığında ardışık aynı etiketleri tekilleştiriyor', () => {
      const points = [
        { price: 100, scrapedAt: '2026-08-11T08:00:00Z' },
        { price: 100, scrapedAt: '2026-08-11T20:00:00Z' },
      ];

      const labels = component.buildXAxisLabels(points);
      const texts = labels.map((l: { label: string }) => l.label);
      const hasConsecutiveDuplicate = texts.some((t: string, i: number) => i > 0 && t === texts[i - 1]);

      expect(hasConsecutiveDuplicate).toBe(false);
    });

    it('tek zaman noktasında tek, ortalanmış bir etiket döner', () => {
      const points = [{ price: 100, scrapedAt: '2026-08-11T08:00:00Z' }];

      const labels = component.buildXAxisLabels(points);

      expect(labels.length).toBe(1);
      expect(labels[0].x).toBe(300); // CHART_WIDTH / 2
    });
  });

  describe('discountEventCount', () => {
    it('ardışık fiyat düşüşlerini sayar, artışları saymaz', () => {
      component.points.set([
        { price: 500, scrapedAt: '2026-08-01T00:00:00Z' },
        { price: 400, scrapedAt: '2026-08-05T00:00:00Z' }, // düşüş
        { price: 450, scrapedAt: '2026-08-08T00:00:00Z' }, // artış
        { price: 350, scrapedAt: '2026-08-12T00:00:00Z' }, // düşüş
      ]);

      expect(component.discountEventCount()).toBe(2);
    });

    it('hiç düşüş yoksa 0 döner', () => {
      component.points.set([
        { price: 100, scrapedAt: '2026-08-01T00:00:00Z' },
        { price: 200, scrapedAt: '2026-08-05T00:00:00Z' },
      ]);

      expect(component.discountEventCount()).toBe(0);
    });
  });

});

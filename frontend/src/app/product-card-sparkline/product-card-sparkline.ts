import { Component, computed, input } from '@angular/core';

import { PricePoint } from '../core/price-history.model';
import { buildAreaPath, buildLinePath, toCoordinates } from '../core/spark-chart';

// Faz 1'de bilinçli olarak ertelenmişti (bkz. CLAUDE.md — N+1 istek riski,
// toplu bir endpoint gerektiriyordu). Ürün kartlarındaki (ana sayfa + marka
// sayfası) küçük fiyat grafiği — hero kartındaki tam sürümün çok daha küçük,
// sabit ölçülü hali. Veri çekme her sayfanın kendi yükleme akışına bağlı
// olduğu için burada değil, çağıran bileşende (deals-list.ts, brand-page.ts).
const CARD_CHART = { width: 100, height: 28, paddingY: 3 };

@Component({
  selector: 'app-product-card-sparkline',
  templateUrl: './product-card-sparkline.html',
})
export class ProductCardSparkline {
  readonly points = input<PricePoint[]>([]);

  protected readonly hasEnoughData = computed(() => this.points().length > 1);

  private readonly coordinates = computed(() => {
    const pts = this.points();
    if (pts.length < 2) return [];
    const prices = pts.map((p) => p.price);
    return toCoordinates(pts, Math.min(...prices), Math.max(...prices), CARD_CHART);
  });

  protected readonly linePath = computed(() => buildLinePath(this.coordinates()));
  protected readonly areaPath = computed(() => buildAreaPath(this.coordinates(), CARD_CHART.height));
}

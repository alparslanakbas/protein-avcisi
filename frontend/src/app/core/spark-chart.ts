import { PricePoint } from './price-history.model';

// product-modal.ts'in tam boyutlu fiyat grafiğiyle ana sayfadaki küçük
// sparkline'ların (hero kartı, ileride ürün kartları) ORTAK, saf koordinat/
// path matematiği — DI'a veya bir bileşen örneğine bağımlı değil, sadece
// (points, min, max, boyut) alıp SVG koordinatı/path string'i üretiyor.
export interface SparkChartDimensions {
  width: number;
  height: number;
  paddingY: number;
}

export function toCoordinates(
  points: PricePoint[],
  min: number,
  max: number,
  { width, height, paddingY }: SparkChartDimensions,
): [number, number][] {
  if (points.length === 0) return [];

  const times = points.map((p) => new Date(p.scrapedAt).getTime());
  const minTime = Math.min(...times);
  const maxTime = Math.max(...times);
  const timeSpan = maxTime - minTime || 1;

  // Fiyat hiç değişmemişse (min===max) çizgi grafiğin dibine yapışıp "boş"
  // görünüyordu — bu durumda görsel aralığı fiyatın etrafında yapay olarak
  // genişletip çizgiyi dikeyde ortalıyoruz.
  let effectiveMin = min;
  let effectiveMax = max;
  if (max - min < 0.01) {
    const padding = Math.max(max * 0.05, 1);
    effectiveMin = min - padding;
    effectiveMax = max + padding;
  }
  const priceSpan = effectiveMax - effectiveMin;

  return points.map((p) => {
    const t = new Date(p.scrapedAt).getTime();
    const x = points.length === 1 ? width / 2 : ((t - minTime) / timeSpan) * width;
    const y = height - paddingY - ((p.price - effectiveMin) / priceSpan) * (height - paddingY * 2);
    return [x, y] as [number, number];
  });
}

export function buildLinePath(coords: [number, number][]): string {
  if (coords.length === 0) return '';
  if (coords.length === 1) {
    const [[x, y]] = coords;
    return `M ${x - 4} ${y} L ${x + 4} ${y}`;
  }
  return coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ');
}

export function buildAreaPath(coords: [number, number][], height: number): string {
  if (coords.length === 0) return '';
  const first = coords[0];
  const last = coords[coords.length - 1];
  const line = coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ');
  return `${line} L ${last[0]} ${height} L ${first[0]} ${height} Z`;
}

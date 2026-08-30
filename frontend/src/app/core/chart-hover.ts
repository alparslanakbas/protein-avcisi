import { PricePoint } from './price-history.model';

/**
 * Fiyat grafiğindeki fare/dokunma etkileşiminin paylaşılan mantığı.
 *
 * NEDEN AYRI DOSYA: bu hesaplar (en yakın nokta bulma, kenarda tooltip
 * hizalama, aynı gün içindeki noktaların etiketi) ürün modalında ÜÇ ayrı
 * üretim hatasına yol açtı — tekrar eden eksen etiketleri, kenardaki
 * tooltip'in kırpılması, aynı gün/aynı fiyat noktalarının hover'da
 * tekrarlaması. İnceleme sayfasına da grafik eklenirken bu mantığı
 * kopyalamak, aynı hataların bir kopyada geri gelmesini garanti ederdi.
 *
 * Grafik ölçüleri parametre: modal 600×220 ızgaralı, inceleme sayfası daha
 * sade ve alçak. Ortak olan matematik, farklı olan görünüm.
 */

// timeZone sabit Europe/Istanbul — kullanıcının cihaz saat dilimine
// bırakılırsa aynı an farklı ziyaretçilere farklı "gün" gösterebilirdi.
// Site yalnızca TR pazarına hizmet ediyor.
const TZ = 'Europe/Istanbul';

const dayFormatter = new Intl.DateTimeFormat('tr-TR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: TZ,
});

// Aynı gün içinde fiyat gerçekten değiştiyse yalnızca tarih yetersiz kalıyor;
// o durumda saat de ekleniyor ki hangi anda değiştiği görünsün.
const dayTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: TZ,
});

export type HoverAlign = 'left' | 'center' | 'right';

/**
 * İmlecin ekran koordinatından en yakın veri noktasının indeksini bulur.
 * SVG `preserveAspectRatio="none"` ile esnediği için ekran genişliğinden
 * viewBox genişliğine ölçekleme şart.
 */
export function nearestPointIndex(
  svg: SVGSVGElement,
  clientX: number,
  coords: readonly [number, number][],
  viewBoxWidth: number,
): number | null {
  if (coords.length === 0) return null;

  const rect = svg.getBoundingClientRect();
  if (rect.width === 0) return null;

  const svgX = (clientX - rect.left) * (viewBoxWidth / rect.width);

  let nearestIndex = 0;
  let nearestDist = Infinity;
  coords.forEach(([x], i) => {
    const dist = Math.abs(x - svgX);
    if (dist < nearestDist) {
      nearestDist = dist;
      nearestIndex = i;
    }
  });
  return nearestIndex;
}

/**
 * Tooltip hizalaması. Kenara yakınken ortalı hizalama tooltip'in yarısını
 * taşırıp kırpılmasına yol açıyordu — kenara yakınsa o yöne yaslıyoruz.
 */
export function hoverAlign(x: number, viewBoxWidth: number): HoverAlign {
  if (x < viewBoxWidth * 0.12) return 'left';
  if (x > viewBoxWidth * 0.88) return 'right';
  return 'center';
}

/** Tooltip'te gösterilecek tarih; aynı güne ait birden fazla nokta varsa saat de eklenir. */
export function tooltipDateLabel(points: readonly PricePoint[], idx: number): string {
  const point = points[idx];
  if (!point) return '';
  const day = dayFormatter.format(new Date(point.scrapedAt));
  const sameDayCount = points.filter(
    (p) => dayFormatter.format(new Date(p.scrapedAt)) === day,
  ).length;
  const formatter = sameDayCount > 1 ? dayTimeFormatter : dayFormatter;
  return formatter.format(new Date(point.scrapedAt));
}

/**
 * Aynı gün + aynı fiyat olan ardışık noktaları teke indirir.
 *
 * Tarama günde birkaç kez çalıştığı için fiyat hiç değişmese bile aynı güne
 * ait çok sayıda nokta birikiyor; hover bunların her birine ayrı ayrı
 * yapışınca kullanıcı art arda aynı tarihi görüyordu. Fiyat o gün içinde
 * gerçekten değiştiyse iki nokta da korunuyor.
 */
export function dedupeSameDaySamePrice(points: readonly PricePoint[]): PricePoint[] {
  const result: PricePoint[] = [];

  for (const point of points) {
    const prev = result[result.length - 1];
    const sameDay =
      prev &&
      dayFormatter.format(new Date(prev.scrapedAt)) ===
        dayFormatter.format(new Date(point.scrapedAt));

    if (prev && sameDay && prev.price === point.price) {
      result[result.length - 1] = point;
    } else {
      result.push(point);
    }
  }

  return result;
}

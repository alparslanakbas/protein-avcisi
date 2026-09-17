import { Pipe, PipeTransform } from '@angular/core';

/** Yalnızca gösterim: API'nin cümlesini JSON kaçışlarıyla göstermeden korur. */
@Pipe({ name: 'yonetimHataSebebi' })
export class YonetimHataSebebi implements PipeTransform {
  transform(reason: string | null): string {
    if (!reason) return '—';
    try {
      const parsed: unknown = JSON.parse(reason);
      if (
        parsed &&
        typeof parsed === 'object' &&
        'message' in parsed &&
        typeof parsed.message === 'string'
      ) {
        return parsed.message;
      }
    } catch {
      // Düz metin yanıtlar zaten gösterilebilir.
    }
    return reason;
  }
}

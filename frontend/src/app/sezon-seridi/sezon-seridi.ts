import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DealsService } from '../core/deals.service';
import { KampanyaSayilari, sezonGeriSayimi } from '../core/kampanya';

/**
 * Ana sayfada, hero'nun altında Efsane Kasım şeridi: geri sayım, /efsane-kasim sayfasıyla aynı ölçümün tek sayısı
 * ve sayfaya bağlantı. Sezon dışında hiç görünmez ve istek de atmaz (sezonGeriSayimi null). Hero'daki eski küçük
 * metin bağlantısının yerine geldi (20 Ekim): sezon sayfasının ana sayfadan görünür olması kararlaştırılmıştı.
 */
@Component({
  selector: 'app-sezon-seridi',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './sezon-seridi.html',
})
export class SezonSeridi {
  private readonly dealsService = inject(DealsService);

  protected readonly geriSayim = sezonGeriSayimi(new Date());
  protected readonly ozet = signal<KampanyaSayilari | null>(null);

  constructor() {
    if (!this.geriSayim) return;
    // Sayı yüklenemezse şerit sayısız görünür; ana sayfada hata göstermeye değmez. Yalnız sayılar isteniyor: tam
    // özet sezonda 1 MiB'ı geçip SSR'ın yanıt sınırına takıldı ve ana sayfayı 15 sn bekletti (6 Kasım).
    this.dealsService.getKampanyaSayilari().subscribe({ next: (ozet) => this.ozet.set(ozet), error: () => {} });
  }
}

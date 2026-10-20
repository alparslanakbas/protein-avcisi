import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DealsService } from '../core/deals.service';
import { KampanyaOzeti, sezonGeriSayimi } from '../core/kampanya';

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
  protected readonly ozet = signal<KampanyaOzeti | null>(null);

  constructor() {
    if (!this.geriSayim) return;
    // Sayı yüklenemezse şerit sayısız görünür; ana sayfada hata göstermeye değmez.
    this.dealsService.getKampanyaOzeti().subscribe({ next: (ozet) => this.ozet.set(ozet), error: () => {} });
  }
}

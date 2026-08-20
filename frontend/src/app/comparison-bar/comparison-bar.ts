import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

import { ComparisonService } from '../core/comparison.service';

// Karşılaştırmaya ürün eklendiğinde ekranın altında beliren sabit çubuk.
// app.html'de site geneli duruyor — seçim hangi sayfada yapılırsa yapılsın
// (ana sayfa, kategori, marka) aynı çubuk görünüyor.
@Component({
  selector: 'app-comparison-bar',
  imports: [],
  templateUrl: './comparison-bar.html',
})
export class ComparisonBar {
  private readonly router = inject(Router);
  protected readonly comparison = inject(ComparisonService);

  protected go(): void {
    const url = this.comparison.comparisonUrl();
    if (url) this.router.navigateByUrl(url);
  }
}

import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { BODY_CALCULATORS } from '../core/body-calculators';
import { CALCULATOR_ICON_PATHS, calculatorIconPath } from '../core/nav-icons';
import { PageMetaService } from '../core/page-meta.service';
import { SUPPLEMENT_DOSAGES } from '../core/supplement-dosages';
import { SiteHeader } from '../site-header/site-header';

interface CalculatorCard {
  path: string;
  title: string;
  description: string;
  iconPath: string;
}

// Hesaplama araçlarının index sayfası — /kategoriler ile aynı desende.
// Nav'daki "Hesaplama" dropdown'ı da buraya ve tek tek araçlara link veriyor.
@Component({
  selector: 'app-calculator-list-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './calculator-list-page.html',
})
export class CalculatorListPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);

  // Beslenme/vücut hesaplayıcıları bir grupta, takviye dozu hesaplayıcıları
  // ayrı bir grupta gösteriliyor — kullanıcı geri bildirimi: kartlar çok
  // sade/tek düzeydi, hem ikon hem gruplama eklendi. İkonlar core/nav-icons.ts'te
  // paylaşılıyor (nav dropdown'larıyla aynı set).
  protected readonly bodyGroupCalculators: CalculatorCard[] = [
    {
      path: '/hesaplama/protein-ihtiyaci',
      title: 'Günlük Protein İhtiyacı',
      description:
        'Kilona ve antrenman yoğunluğuna göre günlük protein hedefini hesapla, servis başı en uygun ürünleri gör.',
      iconPath: CALCULATOR_ICON_PATHS.plate,
    },
    ...BODY_CALCULATORS.map((c): CalculatorCard => ({
      path: `/hesaplama/${c.slug}`,
      title: c.name,
      description: c.description,
      iconPath: calculatorIconPath(c.slug),
    })),
  ];

  protected readonly dosageGroupCalculators: CalculatorCard[] = SUPPLEMENT_DOSAGES.map((s): CalculatorCard => ({
    path: `/hesaplama/${s.slug}`,
    title: `${s.name} Dozu`,
    description: `Günde ${s.minDailyGrams}-${s.maxDailyGrams} g yaygın aralık. Seçtiğin paketin kaç gün yeteceğini ve günlük maliyetini hesapla.`,
    iconPath: CALCULATOR_ICON_PATHS.capsule,
  }));

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Spor Takviyesi Hesaplama Araçları | ProteinAvcısı',
      description:
        'Protein ihtiyacı, kreatin, beta-alanine, sitrülin, betain ve EAA dozu hesaplama araçları — sonuçlar güncel ürün fiyatlarına bağlı.',
      canonicalPath: '/hesaplama',
    });
  }
}

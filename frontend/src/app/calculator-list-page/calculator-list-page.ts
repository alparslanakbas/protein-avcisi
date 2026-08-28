import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { BODY_CALCULATORS } from '../core/body-calculators';
import { calculatorPhosphorIcon } from '../core/nav-icons';
import { PageMetaService } from '../core/page-meta.service';
import { SUPPLEMENT_DOSAGES } from '../core/supplement-dosages';
import { SiteHeader } from '../site-header/site-header';

interface CalculatorCard {
  path: string;
  title: string;
  description: string;
  iconClass: string;
  tone: CalculatorTone;
}

type CalculatorSection = 'body' | 'dosage';
type CalculatorTone = 'violet' | 'mint' | 'blue' | 'cyan' | 'orange' | 'rose';

// Hesaplama araçlarının index sayfası — /kategoriler ile aynı desende.
// Nav'daki "Hesaplama" dropdown'ı da buraya ve tek tek araçlara link veriyor.
@Component({
  selector: 'app-calculator-list-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './calculator-list-page.html',
})
export class CalculatorListPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  protected readonly activeSection = signal<CalculatorSection>('body');

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
      iconClass: calculatorPhosphorIcon('protein-ihtiyaci'),
      tone: 'violet',
    },
    ...BODY_CALCULATORS.map((c, index): CalculatorCard => ({
      path: `/hesaplama/${c.slug}`,
      title: c.name,
      description: c.description,
      iconClass: calculatorPhosphorIcon(c.slug),
      tone: (['mint', 'blue', 'cyan'] as CalculatorTone[])[index] ?? 'violet',
    })),
  ];

  protected readonly dosageGroupCalculators: CalculatorCard[] = SUPPLEMENT_DOSAGES.map((s, index): CalculatorCard => ({
    path: `/hesaplama/${s.slug}`,
    title: `${s.name} Dozu`,
    description: `Günde ${s.minDailyGrams}-${s.maxDailyGrams} g yaygın aralık. Seçtiğin paketin kaç gün yeteceğini ve günlük maliyetini hesapla.`,
    iconClass: calculatorPhosphorIcon(s.slug),
    tone: (['violet', 'mint', 'blue', 'orange', 'rose'] as CalculatorTone[])[index] ?? 'violet',
  }));

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Spor Takviyesi Hesaplama Araçları | ProteinAvcısı',
      description:
        'Protein ihtiyacı, kreatin, beta-alanine, sitrülin, betain ve EAA dozu hesaplama araçları — sonuçlar güncel ürün fiyatlarına bağlı.',
      canonicalPath: '/hesaplama',
    });
  }

  protected selectSection(section: CalculatorSection): void {
    this.activeSection.set(section);
    const target = this.document.getElementById(section === 'body' ? 'beslenme-araclari' : 'takviye-araclari');
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}

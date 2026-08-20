import { DOCUMENT } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ACTIVITY_OPTIONS, BodyCalculator, findBodyCalculator } from '../core/body-calculators';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

// Kalori (TDEE), BMI ve su ihtiyacı için TEK bileşen — hangi girdi
// alanlarının gösterileceği ve hesabın kendisi konfigürasyondan geliyor
// (body-calculators.ts). Üç ayrı bileşen yazmak, formları ve düzeni üç kez
// kopyalamak olurdu; SupplementDosagePage ile aynı yaklaşım.
@Component({
  selector: 'app-body-calculator-page',
  imports: [FormsModule, RouterLink, SiteHeader],
  templateUrl: './body-calculator-page.html',
})
export class BodyCalculatorPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly activityOptions = ACTIVITY_OPTIONS;
  protected readonly config = signal<BodyCalculator | null>(null);

  protected readonly gender = signal<'male' | 'female'>('male');
  protected readonly age = signal<number | null>(null);
  protected readonly height = signal<number | null>(null);
  protected readonly weight = signal<number | null>(null);
  protected readonly activityId = signal<string>('moderate');

  // Girdi eksik ya da makul aralık dışındaysa null — varsayılan bir değerle
  // doldurup "senin sonucun bu" demek yanıltıcı olurdu (protein
  // hesaplayıcısındaki aynı karar).
  protected readonly result = computed(() => {
    const cfg = this.config();
    if (!cfg) return null;

    return cfg.calculate({
      gender: this.gender(),
      age: this.age(),
      height: this.height(),
      weight: this.weight(),
      activityId: this.activityId(),
    });
  });

  protected showsField(field: string): boolean {
    return this.config()?.fields.includes(field as never) ?? false;
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const config = findBodyCalculator(params.get('slug') ?? '');
      if (!config) {
        this.router.navigate(['/hesaplama']);
        return;
      }

      this.config.set(config);
      this.pageMeta.set({
        title: config.title,
        description: config.description,
        canonicalPath: `/hesaplama/${config.slug}`,
      });

      this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, {
        '@context': 'https://schema.org',
        '@type': 'WebApplication',
        name: config.h1,
        applicationCategory: 'HealthApplication',
        operatingSystem: 'Web',
        offers: { '@type': 'Offer', price: '0', priceCurrency: 'TRY' },
      });
    });
  }
}

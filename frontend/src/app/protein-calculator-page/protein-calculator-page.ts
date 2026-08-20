import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { DOCUMENT } from '@angular/common';
import { slugify } from '../core/slugify';
import { SiteHeader } from '../site-header/site-header';

interface ActivityLevel {
  id: string;
  label: string;
  description: string;
  // Kilo başına gram protein aralığı — sektörde ve beslenme kaynaklarında
  // yaygın kabul gören değerler, uydurma değil. Tek bir sayı yerine ARALIK
  // veriyoruz çünkü gerçekte de tek bir doğru sayı yok.
  minPerKg: number;
  maxPerKg: number;
}

const ACTIVITY_LEVELS: ActivityLevel[] = [
  {
    id: 'sedentary',
    label: 'Hareketsiz',
    description: 'Düzenli spor yapmıyorum',
    minPerKg: 0.8,
    maxPerKg: 1.0,
  },
  {
    id: 'active',
    label: 'Düzenli egzersiz',
    description: 'Haftada 3-5 gün antrenman',
    minPerKg: 1.2,
    maxPerKg: 1.6,
  },
  {
    id: 'intense',
    label: 'Yoğun antrenman',
    description: 'Kas kazanımı odaklı, haftada 5+ gün',
    minPerKg: 1.6,
    maxPerKg: 2.2,
  },
];

interface ProductValue {
  deal: Deal;
  pricePerServing: number;
  proteinServings: number;
}

// Bu sayfanın fikri: "günlük protein ihtiyacı hesaplama" araması yüksek
// hacimli ve rakiplerin çoğunda bir hesaplayıcı var — ama hiçbirinde CANLI
// fiyat verisi yok. Bizde ikisi de olduğu için hesaplama sonucunu doğrudan
// "servis başı en uygun ürün" listesine bağlayabiliyoruz. Öneri listesi
// yalnızca porsiyon büyüklüğü GERÇEKTEN bilinen ürünlerden kuruluyor —
// "30 gr = 1 servis" gibi bir varsayım bu projede hiç yapılmadı.
@Component({
  selector: 'app-protein-calculator-page',
  imports: [DecimalPipe, FormsModule, RouterLink, SiteHeader],
  templateUrl: './protein-calculator-page.html',
})
export class ProteinCalculatorPage implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly activityLevels = ACTIVITY_LEVELS;

  protected readonly weight = signal<number | null>(null);
  protected readonly activityId = signal<string>('active');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  private readonly products = signal<Deal[]>([]);

  protected readonly selectedActivity = computed(
    () => ACTIVITY_LEVELS.find((level) => level.id === this.activityId()) ?? ACTIVITY_LEVELS[1],
  );

  // Kilo girilmeden hiçbir sonuç göstermiyoruz — varsayılan bir kiloyla
  // (ör. 70 kg) doldurup "senin ihtiyacın şu" demek yanıltıcı olurdu.
  protected readonly dailyProtein = computed(() => {
    const kg = this.weight();
    if (!kg || kg <= 0 || kg > 400) return null;

    const level = this.selectedActivity();
    return {
      min: Math.round(kg * level.minPerKg),
      max: Math.round(kg * level.maxPerKg),
    };
  });

  // Backend zaten servis başı fiyata göre sıralı ve elenmiş bir liste
  // döndürüyor (bkz. GetBestValuePerServingAsync); burada sadece gösterim
  // için servis sayısı/birim fiyat tekrar hesaplanıyor.
  protected readonly bestValueProducts = computed<ProductValue[]>(() =>
    this.products()
      .map((deal) => {
        const packageGrams = this.parsePackageGrams(deal.size);
        if (!packageGrams || !deal.servingSizeGrams || deal.servingSizeGrams <= 0) return null;

        const servings = packageGrams / deal.servingSizeGrams;
        if (!servings || servings < 1) return null;

        return {
          deal,
          pricePerServing: deal.currentPrice / servings,
          proteinServings: servings,
        };
      })
      .filter((item): item is ProductValue => item !== null),
  );

  protected productLink(deal: Deal): string[] {
    return ['/urun', String(deal.productId), slugify(deal.productName)];
  }

  private parsePackageGrams(size: string | null): number | null {
    if (!size) return null;
    const match = /^(\d+(?:[.,]\d+)?)\s*(Gr|Kg)$/i.exec(size.trim());
    if (!match) return null;

    const value = Number(match[1].replace(',', '.'));
    if (!value) return null;

    return match[2].toLowerCase() === 'kg' ? value * 1000 : value;
  }

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Günlük Protein İhtiyacı Hesaplama | ProteinAvcısı',
      description:
        'Kilona ve antrenman yoğunluğuna göre günlük protein ihtiyacını hesapla, sonucu doğrudan güncel fiyatlarla karşılaştır — servis başı en uygun protein tozu ürünlerini gör.',
      canonicalPath: '/hesaplama/protein-ihtiyaci',
    });

    // Google'ın hesaplayıcı sayfalarını anlamasına yardımcı olan yapısal veri.
    this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, {
      '@context': 'https://schema.org',
      '@type': 'WebApplication',
      name: 'Günlük Protein İhtiyacı Hesaplama',
      applicationCategory: 'HealthApplication',
      operatingSystem: 'Web',
      offers: { '@type': 'Offer', price: '0', priceCurrency: 'TRY' },
    });

    // Backend hesaplayıp sıralıyor, yalnızca gösterilecek 6 ürün geliyor.
    this.dealsService.getBestValuePerServing('protein-tozu', 6).subscribe({
      next: (items) => {
        this.products.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }
}

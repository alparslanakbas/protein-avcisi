import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { setCanonicalLink } from '../core/canonical-link';
import { CATEGORY_LABELS } from '../core/category-labels';
import { Deal } from '../core/deal.model';
import { DealsService } from '../core/deals.service';

// Bir geliştiricinin SEO geri bildirimi üzerine eklendi: kategori sayfaları
// marka sayfalarıyla aynı desende ama şablon metin yerine her kategori için
// gerçekten farklı, açıklayıcı bir giriş metni taşıyor — "ince içerik"
// (thin content) riskini azaltmak için bilinçli.
const CATEGORY_INTROS: Record<string, string> = {
  'protein-tozu':
    'Protein tozu, kas gelişimi ve günlük protein ihtiyacını karşılamak için en yaygın kullanılan spor takviyesi. Whey (peynir altı suyu) protein hızlı emilimiyle bilinirken, kazein daha yavaş salınım sağlar.',
  kreatin:
    'Kreatin, güç ve patlayıcı performans gerektiren antrenmanlarda en çok araştırılmış takviyelerden biri. Genellikle kreatin monohidrat formunda satılır ve düzenli günlük kullanımla etkisini gösterir.',
  'amino-asitler':
    'BCAA, EAA, glutamin ve arginin gibi amino asit takviyeleri, kas onarımı ve antrenman sonrası toparlanma sürecini desteklemek amacıyla tercih ediliyor.',
  'pre-workout':
    'Pre-workout ürünleri, antrenman öncesi enerji ve odaklanmayı artırmak amacıyla kafein, beta-alanin ve nitrik oksit destekleyici içerikler barındırır.',
  'yag-yakici':
    'Yağ yakıcı takviyeler, termojenik bileşenler içeren ve diyet ile antrenman programını desteklemek amacıyla kullanılan ürünler.',
  'kilo-hacim':
    'Kilo/hacim (gainer) ürünleri, yüksek kalori ve karbonhidrat içeriğiyle kilo almakta zorlanan veya hacim antrenmanı yapan kullanıcılar için tasarlandı.',
  vitamin:
    'Vitamin ve mineral takviyeleri, günlük beslenmedeki eksiklikleri tamamlamak amacıyla kullanılır — multivitaminden omega-3\'e, magnezyumdan çinkoya kadar geniş bir yelpazeyi kapsar.',
  'saglikli-atistirmaliklar':
    'Protein bar, kurabiye ve pirinç pastası gibi sağlıklı atıştırmalıklar, günlük protein/kalori hedefini pratik bir şekilde tamamlamak isteyenler için alternatif sunuyor.',
  'l-carnitine-cla':
    'L-Carnitine ve CLA (konjuge linoleik asit), yağ metabolizmasını desteklemek amacıyla kullanılan, genellikle diyet döneminde tercih edilen takviyeler.',
};

@Component({
  selector: 'app-category-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './category-page.html',
})
export class CategoryPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dealsService = inject(DealsService);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  protected readonly categorySlug = signal<string>('');
  protected readonly categoryLabel = signal<string>('');
  protected readonly categoryIntro = signal<string>('');
  protected readonly deals = signal<Deal[]>([]);
  protected readonly storeDeals = signal<Deal[]>([]);
  protected readonly otherCategories = signal<{ slug: string; label: string }[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('categorySlug') ?? '';
      this.loadCategory(slug);
    });
  }

  private loadCategory(slug: string): void {
    this.loading.set(true);

    this.dealsService.getFilterOptions().subscribe({
      next: (options) => {
        const match = options.categories.find((c) => c.toLowerCase() === slug.toLowerCase());
        const label = match ? CATEGORY_LABELS[match] : undefined;
        if (!match || !label) {
          // Soft-404 yerine gerçek yönlendirme (bkz. marka sayfalarındaki aynı karar).
          this.router.navigate(['/']);
          return;
        }

        this.categorySlug.set(match);
        this.categoryLabel.set(label);
        this.categoryIntro.set(CATEGORY_INTROS[match]);
        this.otherCategories.set(
          options.categories
            .filter((c) => c !== match)
            .map((c) => ({ slug: c, label: CATEGORY_LABELS[c] ?? c })),
        );
        this.setMeta(label);
        setCanonicalLink(this.document, `/kategori/${match}`);

        this.dealsService.getDeals({ categories: [match], pageSize: 12 }).subscribe((result) => {
          this.deals.set(result.items);
        });

        this.dealsService.getStoreDeals({ categories: [match], pageSize: 12 }).subscribe({
          next: (result) => {
            this.storeDeals.set(result.items);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  private setMeta(label: string): void {
    const title = `${label} Fiyatları ve İndirimleri | ProteinAvcısı`;
    const description = `${label} kategorisindeki güncel fiyatlar, gerçek fiyat geçmişine dayanan doğrulanmış indirimler ve mağaza kampanyaları. ProteinAvcısı, fiyatları düzenli olarak takip ediyor.`;

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    this.metaService.updateTag({ property: 'og:title', content: title });
    this.metaService.updateTag({ property: 'og:description', content: description });
    this.metaService.updateTag({ property: 'og:type', content: 'website' });
  }

  protected discountBadge(deal: Deal): string {
    return `-%${deal.discountPercent}`;
  }

  protected storeDiscountBadge(deal: Deal): string {
    return `Mağaza -%${deal.storeDiscountPercent}`;
  }

  protected goToProduct(deal: Deal): void {
    this.router.navigate(['/urun', deal.productId]);
  }
}

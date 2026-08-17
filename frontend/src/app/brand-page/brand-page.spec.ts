import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ThemeService } from '../core/theme.service';
import { BrandPage } from './brand-page';

// comparisonPairSlug marka karşılaştırma sayfalarının kanonik URL'ini
// üretiyor — sıra ne olursa olsun aynı çift için aynı URL'e çıkması
// (duplicate content riskini önlemek için bilinçli tasarlandı) buranın
// tek gerçek davranışsal garantisi, regresyon testi burada değerli.
describe('BrandPage - comparisonPairSlug', () => {
  let component: any;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BrandPage],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({})) } },
        // BrandPage kendi başına ThemeService'e bağlı değil ama şablonundaki
        // <app-site-header> child component'i bağlı — TestBed.createComponent
        // detectChanges() çağrılmasa bile şablondaki child component'leri view
        // oluşturma aşamasında instantiate ediyor, bu yüzden SiteHeader'ın
        // gerçek ThemeService constructor'ı (window.matchMedia çağırıyor,
        // jsdom'da yok) burada da tetikleniyor. comparisonPairSlug'ın temayla
        // hiç ilgisi yok, minimal bir sahte ile değiştiriliyor.
        { provide: ThemeService, useValue: { preference: signal('system') } },
      ],
    });
    component = TestBed.createComponent(BrandPage).componentInstance;
  });

  it('iki markayı alfabetik sırayla birleştirir', () => {
    component.brandName.set('SSN');

    expect(component.comparisonPairSlug('HIQ')).toBe('hiq-vs-ssn');
  });

  it('hangi marka "mevcut" hangisi "diğer" olursa olsun aynı kanonik URL üretir', () => {
    component.brandName.set('hiq');
    const a = component.comparisonPairSlug('ssn');

    component.brandName.set('ssn');
    const b = component.comparisonPairSlug('hiq');

    expect(a).toBe(b);
  });
});

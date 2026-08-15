import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

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

import { describe, expect, it } from 'vitest';
import { brandSlug, resolveBrandFromSlug } from './brand-slug';

const BRANDS = ['HIQ', 'Hardline', 'ProteinOcean', 'SSN', 'Torq Nutrition', 'West Nutrition', 'Yeşilmarka'];

describe('brandSlug', () => {
  it('boşlukları tireye çevirir', () => {
    expect(brandSlug('Torq Nutrition')).toBe('torq-nutrition');
    expect(brandSlug('West Nutrition')).toBe('west-nutrition');
  });

  it('Türkçe karakterleri çevirir', () => {
    expect(brandSlug('Yeşilmarka')).toBe('yesilmarka');
  });

  it('tek kelimeli markalarda adres değişmiyor', () => {
    // Bu adresler zaten dizinde; slug'a geçiş onları bozmamalı.
    expect(brandSlug('Hardline')).toBe('hardline');
    expect(brandSlug('HIQ')).toBe('hiq');
  });
});

describe('resolveBrandFromSlug', () => {
  it('slug\'dan gerçek marka adını bulur', () => {
    expect(resolveBrandFromSlug('torq-nutrition', BRANDS)).toBe('Torq Nutrition');
    expect(resolveBrandFromSlug('hardline', BRANDS)).toBe('Hardline');
  });

  // Bir süre sitemap'te boşluklu adresler yer aldı, kırılmamalılar.
  it('boşluklu ve Türkçe karakterli eski adresleri de çözer', () => {
    expect(resolveBrandFromSlug('torq nutrition', BRANDS)).toBe('Torq Nutrition');
    expect(resolveBrandFromSlug('yeşilmarka', BRANDS)).toBe('Yeşilmarka');
  });

  it('bilinmeyen markada null döner', () => {
    expect(resolveBrandFromSlug('bilinmeyen-marka', BRANDS)).toBeNull();
  });
});

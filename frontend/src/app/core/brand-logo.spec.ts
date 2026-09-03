import { describe, expect, it } from 'vitest';

import { brandLogoUrl, brandMonogram, brandMonogramColor } from './brand-logo';

describe('brandLogoUrl', () => {
  it('logosu indirilmiş markada yerel yolu döndürür', () => {
    expect(brandLogoUrl('HIQ')).toBe('/marka-logo/hiq.webp');
    expect(brandLogoUrl('Space Gym Supplements')).toBe('/marka-logo/space-gym-supplements.webp');
    expect(brandLogoUrl('Yeşilmarka')).toBe('/marka-logo/yesilmarka.webp');
    // Kesme işareti slug'da tire oluyor — dosya adıyla birebir uyuşmalı.
    expect(brandLogoUrl("Gigi's")).toBe('/marka-logo/gigi-s.webp');
    expect(brandLogoUrl('MLA Protein')).toBe('/marka-logo/mla-protein.webp');
  });

  // Katalogdaki 66 marka yalnızca bir bayiden geliyor; onlar için doğrulanmış
  // bir logo kaynağı YOK ve uydurulmuyor.
  it('logosu olmayan markada null döndürür', () => {
    expect(brandLogoUrl('Olimp')).toBeNull();
    expect(brandLogoUrl('BioTech USA')).toBeNull();
    expect(brandLogoUrl('Muscle Pump')).toBeNull();
  });

  // Yol brandSlug ile üretiliyor; dosya adlarıyla birebir aynı olmak zorunda.
  it('Türkçe karakterli adı dosya adıyla aynı sluga çevirir', () => {
    expect(brandLogoUrl('Yeşilmarka')).toContain('yesilmarka');
  });
});

describe('brandMonogram', () => {
  it('iki kelimeli adda iki baş harfi alır', () => {
    expect(brandMonogram('Nuclear Nutrition')).toBe('NN');
    expect(brandMonogram('Bad Ass')).toBe('BA');
  });

  it('tek kelimeli adda ilk iki harfi alır', () => {
    expect(brandMonogram('Olimp')).toBe('OL');
    expect(brandMonogram('Grenade')).toBe('GR');
  });

  // Türkçe büyütme tuzağı: "i" -> "İ" olmalı, "I" değil.
  it('Türkçe büyütme kuralına uyar', () => {
    expect(brandMonogram('ironMaxx')).toBe('İR');
    expect(brandMonogram('Sixpack')).toBe('Sİ');
  });

  it('tire ve noktayı kelime sınırı sayar', () => {
    expect(brandMonogram('Z-Konzept')).toBe('ZK');
    expect(brandMonogram('Sci-Tech')).toBe('ST');
  });

  it('boş ada çakılmaz', () => {
    expect(brandMonogram('')).toBe('?');
    expect(brandMonogram('   ')).toBe('?');
  });
});

describe('brandMonogramColor', () => {
  // Rastgele DEĞİL: aynı marka her açılışta aynı rengi almalı, yoksa
  // kullanıcı markayı renginden tanıyamaz.
  it('aynı marka için her zaman aynı rengi verir', () => {
    expect(brandMonogramColor('Olimp')).toBe(brandMonogramColor('Olimp'));
    expect(brandMonogramColor('Dymatize')).toBe(brandMonogramColor('Dymatize'));
  });

  it('geçerli bir hex renk döndürür', () => {
    for (const ad of ['Olimp', 'Dymatize', 'Trec', 'QNT', 'Bahs', '']) {
      expect(brandMonogramColor(ad)).toMatch(/^#[0-9a-f]{6}$/);
    }
  });
});

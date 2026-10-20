import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';

import { YonetimOncelikliEksikler } from './oncelikli-eksikler';
import { YonetimOncelikliUrun, YonetimService } from './yonetim.service';

function urun(over: Partial<YonetimOncelikliUrun>): YonetimOncelikliUrun {
  return {
    id: 1, name: 'Whey 2000 Gr', marka: 'Hardline', seller: null, isActive: true, latestPrice: 1999,
    category: null, categoryIsManual: false, nutritionJson: null, nutritionIsManual: false,
    servingSizeGrams: null, servingsPerPackage: null, tiklanma: 12, ...over,
  };
}

function kur(platform: 'browser' | 'server', cevap = of({ urunler: [urun({})], toplam: 40 })) {
  const api = { oncelikliEksikler: vi.fn(() => cevap) };
  TestBed.configureTestingModule({
    providers: [
      { provide: YonetimService, useValue: api },
      { provide: PLATFORM_ID, useValue: platform },
    ],
  });
  const fixture = TestBed.createComponent(YonetimOncelikliEksikler);
  fixture.detectChanges();
  return { fixture, api, el: fixture.nativeElement as HTMLElement };
}

describe('YonetimOncelikliEksikler', () => {
  it('tarayıcıda listeyi yükler, eksik alanları ve toplamı gösterir', () => {
    const { el, api } = kur('browser');
    expect(api.oncelikliEksikler).toHaveBeenCalledTimes(1);
    expect(el.textContent).toContain('Whey 2000 Gr');
    expect(el.textContent).toContain('Kategori eksik');
    expect(el.textContent).toContain('Besin eksik');
    expect(el.textContent).toContain('Toplam 40 ürün, ilk 1 tanesi gösteriliyor.');
  });

  it('sunucuda istek atmaz', () => {
    const { api } = kur('server');
    expect(api.oncelikliEksikler).not.toHaveBeenCalled();
  });

  it('Düzenle ürünü yayınlar', () => {
    const { fixture, el } = kur('browser');
    const yayinlanan: unknown[] = [];
    fixture.componentInstance.duzenle.subscribe((u) => yayinlanan.push(u));
    el.querySelector<HTMLButtonElement>('li button')!.click();
    expect(yayinlanan).toEqual([expect.objectContaining({ id: 1 })]);
  });

  it('"tablosu yok" diye kapatılan besini eksik saymaz', () => {
    const { fixture } = kur('server');
    const c = fixture.componentInstance;
    expect(c.eksikler(urun({ category: 'protein-tozu', nutritionIsManual: true }))).toEqual([]);
    expect(c.eksikler(urun({ category: 'protein-tozu' }))).toEqual(['Besin']);
  });

  it('hata mesajı gösterir', () => {
    const { el } = kur('browser', throwError(() => new Error('x')));
    expect(el.textContent).toContain('Öncelikli eksikler yüklenemedi.');
  });
});

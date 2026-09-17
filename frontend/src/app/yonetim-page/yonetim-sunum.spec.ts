import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { YonetimDuzenleyiciOdagi } from './duzenleyici-odagi';
import { YonetimHataSebebi } from './hata-sebebi';

@Component({
  imports: [YonetimDuzenleyiciOdagi],
  template: `<button id="acan" (click)="acik.set(true)">Veriyi düzenle</button>
    @if (acik()) {
      <section yonetimDuzenleyiciOdagi role="dialog">
        <button id="ilk">Kapat</button><input aria-label="Miktar" />
        <button id="kapali" disabled>Yok</button><button id="son">Kaydet</button>
      </section>
    }`,
})
class DuzenleyiciDuzenegi {
  readonly acik = signal(false);
}

describe('Yönetim sunumu', () => {
  const pipe = new YonetimHataSebebi();

  it('API mesajını anlamını değiştirmeden JSON kabuğundan çıkarır', () => {
    expect(pipe.transform('{"message":"\\u0027Kafein\\u0027: birim geçersiz"}')).toBe(
      "'Kafein': birim geçersiz",
    );
  });

  it('düz metni ve mesajsız yapıları olduğu gibi bırakır', () => {
    expect(pipe.transform('Ürün bulunamadı.')).toBe('Ürün bulunamadı.');
    expect(pipe.transform('{"error":"Bilinmiyor"}')).toBe('{"error":"Bilinmiyor"}');
    expect(pipe.transform('{"message":23}')).toBe('{"message":23}');
    expect(pipe.transform(null)).toBe('—');
  });

  it('odağı içeri alır, Tab ile dışarı kaçırmaz, kapanınca odağı ve kaydırmayı geri verir', async () => {
    const fixture = TestBed.createComponent(DuzenleyiciDuzenegi);
    fixture.detectChanges();
    const acan: HTMLButtonElement = fixture.nativeElement.querySelector('#acan');
    acan.focus();
    const oncekiTasma = document.body.style.overflow;
    acan.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const pencere: HTMLElement = fixture.nativeElement.querySelector('[role="dialog"]');
    const ilk = pencere.querySelector<HTMLElement>('#ilk')!;
    const son = pencere.querySelector<HTMLElement>('#son')!;
    expect(document.activeElement).toBe(pencere);
    expect(document.body.style.overflow).toBe('hidden');

    pencere.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(ilk);
    ilk.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true, cancelable: true }),
    );
    expect(document.activeElement).toBe(son);
    son.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(ilk);

    fixture.componentInstance.acik.set(false);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.activeElement).toBe(acan);
    expect(document.body.style.overflow).toBe(oncekiTasma);
    fixture.destroy();
  });
});

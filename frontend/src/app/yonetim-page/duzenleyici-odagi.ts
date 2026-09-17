import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import {
  AfterViewInit,
  Directive,
  ElementRef,
  OnDestroy,
  PLATFORM_ID,
  inject,
} from '@angular/core';

/** Klavye odağını ve sayfa kaydırmasını düzenleyicinin içinde tutar; ürün verisi ve kaydetme sayfada kalır. */
@Directive({
  selector: '[yonetimDuzenleyiciOdagi]',
  host: { tabindex: '-1', '(keydown)': 'containFocus($event)' },
})
export class YonetimDuzenleyiciOdagi implements AfterViewInit, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private previous: HTMLElement | null = null;
  private overflow = '';
  private destroyed = false;

  ngAfterViewInit(): void {
    if (!this.browser) return;
    this.previous = this.document.activeElement as HTMLElement | null;
    this.overflow = this.document.body.style.overflow;
    this.document.body.style.overflow = 'hidden';
    queueMicrotask(() => {
      if (!this.destroyed) this.host.nativeElement.focus();
    });
  }

  containFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const root = this.host.nativeElement;
    const controls = [
      ...root.querySelectorAll<HTMLElement>(
        'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex="0"]',
      ),
    ]
      .filter(
        (element) =>
          !element.hidden &&
          element.tabIndex >= 0 &&
          element.getAttribute('aria-hidden') !== 'true',
      )
      .sort((a, b) => (a.compareDocumentPosition(b) & 2 ? 1 : -1));
    const first = controls[0];
    const last = controls.at(-1);
    const active = this.document.activeElement;
    if (!first) {
      event.preventDefault();
      root.focus();
    } else if (event.shiftKey && (active === first || active === root)) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && (active === last || active === root)) {
      event.preventDefault();
      first.focus();
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (!this.browser) return;
    this.document.body.style.overflow = this.overflow;
    const previous = this.previous;
    queueMicrotask(() => {
      if (previous?.isConnected) previous.focus();
    });
  }
}

import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  PLATFORM_ID,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { Deal } from '../core/deal.model';
import { displayName } from '../core/display-name';
import { productPath } from '../core/product-link';

type PreferenceGroup = 'all' | 'performance' | 'nutrition' | 'weight';

interface PreferenceTab {
  id: PreferenceGroup;
  label: string;
  categories: readonly string[];
}

const PREFERENCE_TABS: readonly PreferenceTab[] = [
  { id: 'all', label: 'Tümü', categories: [] },
  { id: 'performance', label: 'Performans', categories: ['amino-asitler', 'kreatin', 'pre-workout'] },
  { id: 'nutrition', label: 'Beslenme', categories: ['protein-tozu', 'vitamin', 'saglikli-atistirmaliklar'] },
  { id: 'weight', label: 'Kilo Kontrolü', categories: ['yag-yakici', 'l-carnitine-cla', 'kilo-hacim'] },
];

/**
 * Backend'de gerçek tercih sıralaması hazırlanana kadar aday ürünleri her gün
 * farklı, fakat SSR ve tarayıcıda aynı kalacak şekilde dağıtır. Math.random()
 * hydration sırasında sunucu ve tarayıcı sırasını farklılaştıracağı için
 * ürün id'si + Türkiye tarihinden deterministik bir skor üretiyoruz.
 */
function dailyProductScore(productId: number, dayKey: string): number {
  let hash = 2166136261;
  const input = `${dayKey}:${productId}`;
  for (let i = 0; i < input.length; i += 1) {
    hash ^= input.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

@Component({
  selector: 'app-preferred-products',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './preferred-products.html',
})
export class PreferredProducts {
  readonly products = input.required<Deal[]>();

  protected readonly displayName = displayName;
  protected readonly productPath = productPath;
  protected readonly tabs = PREFERENCE_TABS;
  protected readonly selectedGroup = signal<PreferenceGroup>('all');
  protected readonly rail = viewChild<ElementRef<HTMLElement>>('rail');

  private readonly destroyRef = inject(DestroyRef);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly dailyShuffleKey = new Intl.DateTimeFormat('en-CA', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    timeZone: 'Europe/Istanbul',
  }).format(new Date());
  private autoSlideHandle: ReturnType<typeof setInterval> | null = null;
  private paused = false;

  protected readonly visibleProducts = computed(() => {
    const activeTab = PREFERENCE_TABS.find((tab) => tab.id === this.selectedGroup()) ?? PREFERENCE_TABS[0];
    const candidates = activeTab.categories.length === 0
      ? this.products()
      : this.products().filter((product) => product.category && activeTab.categories.includes(product.category));

    return [...candidates]
      .sort((left, right) => dailyProductScore(left.productId, this.dailyShuffleKey) - dailyProductScore(right.productId, this.dailyShuffleKey))
      .slice(0, 12);
  });

  constructor() {
    afterNextRender(() => {
      if (!this.isBrowser || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
      this.startAutoSlide();
    });
    this.destroyRef.onDestroy(() => this.stopAutoSlide());
  }

  protected selectGroup(group: PreferenceGroup): void {
    if (this.selectedGroup() === group) return;
    this.selectedGroup.set(group);
    requestAnimationFrame(() => this.rail()?.nativeElement.scrollTo({ left: 0, behavior: 'smooth' }));
  }

  protected scroll(direction: -1 | 1): void {
    const rail = this.rail()?.nativeElement;
    if (!rail) return;
    const card = rail.querySelector<HTMLElement>('.pa-preferred-card');
    const distance = (card?.offsetWidth ?? Math.min(340, rail.clientWidth * 0.85)) + 12;
    rail.scrollBy({ left: distance * direction, behavior: 'smooth' });
  }

  protected pauseAutoSlide(): void {
    this.paused = true;
  }

  protected resumeAutoSlide(): void {
    this.paused = false;
  }

  private startAutoSlide(): void {
    this.stopAutoSlide();
    this.autoSlideHandle = setInterval(() => {
      if (this.paused || document.visibilityState !== 'visible') return;
      const rail = this.rail()?.nativeElement;
      if (!rail || rail.scrollWidth <= rail.clientWidth) return;

      const nearEnd = rail.scrollLeft + rail.clientWidth >= rail.scrollWidth - 24;
      if (nearEnd) {
        rail.scrollTo({ left: 0, behavior: 'smooth' });
      } else {
        this.scroll(1);
      }
    }, 4200);
  }

  private stopAutoSlide(): void {
    if (!this.autoSlideHandle) return;
    clearInterval(this.autoSlideHandle);
    this.autoSlideHandle = null;
  }
}

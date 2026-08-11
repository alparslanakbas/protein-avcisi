import { Injectable, PLATFORM_ID, effect, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type ThemePreference = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'theme-preference';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly preference = signal<ThemePreference>(this.readStoredPreference());

  // SSR'da window/matchMedia yok — sunucuda her zaman "koyu değil" varsayıyoruz,
  // istemci hydrate olunca gerçek sistem tercihiyle düzeltiliyor.
  private readonly systemPrefersDark = this.isBrowser ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  constructor() {
    this.systemPrefersDark?.addEventListener('change', () => {
      if (this.preference() === 'system') {
        this.applyTheme();
      }
    });

    effect(() => {
      const pref = this.preference();
      if (this.isBrowser) {
        localStorage.setItem(STORAGE_KEY, pref);
      }
      this.applyTheme();
    });
  }

  setPreference(preference: ThemePreference): void {
    this.preference.set(preference);
  }

  private isDark(): boolean {
    const pref = this.preference();
    return pref === 'dark' || (pref === 'system' && (this.systemPrefersDark?.matches ?? false));
  }

  private applyTheme(): void {
    if (!this.isBrowser) return;
    document.documentElement.classList.toggle('dark', this.isDark());
  }

  private readStoredPreference(): ThemePreference {
    if (!this.isBrowser) return 'system';
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
  }
}

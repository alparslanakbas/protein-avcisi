import { Injectable, effect, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'theme-preference';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preference = signal<ThemePreference>(this.readStoredPreference());

  private readonly systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)');

  constructor() {
    this.systemPrefersDark.addEventListener('change', () => {
      if (this.preference() === 'system') {
        this.applyTheme();
      }
    });

    effect(() => {
      const pref = this.preference();
      localStorage.setItem(STORAGE_KEY, pref);
      this.applyTheme();
    });
  }

  setPreference(preference: ThemePreference): void {
    this.preference.set(preference);
  }

  private isDark(): boolean {
    const pref = this.preference();
    return pref === 'dark' || (pref === 'system' && this.systemPrefersDark.matches);
  }

  private applyTheme(): void {
    document.documentElement.classList.toggle('dark', this.isDark());
  }

  private readStoredPreference(): ThemePreference {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
  }
}

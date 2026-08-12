import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, computed, inject, input, signal } from '@angular/core';

@Component({
  selector: 'app-share-button',
  imports: [],
  templateUrl: './share-button.html',
})
export class ShareButton {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Verilmezse mevcut sayfanın kendisi paylaşılır (site geneli paylaşım butonu).
  readonly title = input<string>('ProteinAvcısı — Spor Takviyesi Fiyat Takibi');
  readonly shareUrl = input<string | undefined>(undefined);

  protected readonly menuOpen = signal(false);
  protected readonly copied = signal(false);

  private readonly resolvedUrl = computed(() => this.shareUrl() ?? (this.isBrowser ? this.document.location.href : ''));

  protected readonly whatsappUrl = computed(
    () => `https://wa.me/?text=${encodeURIComponent(`${this.title()} ${this.resolvedUrl()}`)}`,
  );
  protected readonly twitterUrl = computed(
    () => `https://twitter.com/intent/tweet?text=${encodeURIComponent(this.title())}&url=${encodeURIComponent(this.resolvedUrl())}`,
  );
  protected readonly facebookUrl = computed(
    () => `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(this.resolvedUrl())}`,
  );

  protected async onShareClick(): Promise<void> {
    if (this.isBrowser && typeof navigator.share === 'function') {
      try {
        await navigator.share({ title: this.title(), url: this.resolvedUrl() });
      } catch {
        // Kullanıcı paylaşım penceresini kapattıysa sessizce geç.
      }
      return;
    }

    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected async copyLink(): Promise<void> {
    if (!this.isBrowser) return;
    await navigator.clipboard.writeText(this.resolvedUrl());
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }
}

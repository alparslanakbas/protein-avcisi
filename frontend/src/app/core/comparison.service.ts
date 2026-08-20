import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';

import { Deal } from './deal.model';

const STORAGE_KEY = 'comparison-slots';

// Karşılaştırmaya alınan ürünün, sayfalar arası gezinirken tekrar API'den
// çekilmesine gerek kalmadan alt çubukta gösterilebilmesi için sakladığımız
// asgari bilgi. Tam Deal nesnesini saklamıyoruz — fiyat gibi alanlar
// bayatlar; karşılaştırma sayfası verinin tamamını taze çekiyor.
export interface ComparisonSlot {
  productId: number;
  productName: string;
  brandName: string;
  imageUrl: string | null;
}

// En fazla iki ürün — yan yana okunabilir bir karşılaştırma için doğru
// sayı bu; üç sütun mobilde okunmaz hale geliyor.
const MAX_SLOTS = 2;

// Karşılaştırma seçimi localStorage'da tutuluyor: kullanıcı ana sayfada bir
// ürün seçip kategori sayfasına geçtiğinde seçimi kaybetmesin (kullanıcı
// isteği: "ilk koyulan işaret saklanmalı"). ThemeService/CookieConsentService
// ile aynı SSR-güvenli desen.
@Injectable({ providedIn: 'root' })
export class ComparisonService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Tüm bileşenler (kartlar, alt çubuk) bu TEK signal'i okuyor — bir yerde
  // yapılan seçim her yerde anında görünsün diye servis seviyesinde.
  readonly slots = signal<ComparisonSlot[]>(this.readFromStorage());
  readonly isFull = computed(() => this.slots().length >= MAX_SLOTS);

  isSelected(productId: number): boolean {
    return this.slots().some((s) => s.productId === productId);
  }

  // Aynı ürüne tekrar basmak seçimi kaldırıyor (toggle) — ayrı bir "çıkar"
  // butonu aramak zorunda kalmasın.
  toggle(deal: Deal): void {
    const current = this.slots();
    if (current.some((s) => s.productId === deal.productId)) {
      this.write(current.filter((s) => s.productId !== deal.productId));
      return;
    }

    if (current.length >= MAX_SLOTS) return;

    this.write([
      ...current,
      {
        productId: deal.productId,
        productName: deal.productName,
        brandName: deal.brandName,
        imageUrl: deal.imageUrl,
      },
    ]);
  }

  remove(productId: number): void {
    this.write(this.slots().filter((s) => s.productId !== productId));
  }

  clear(): void {
    this.write([]);
  }

  // Karşılaştırma sayfasının kanonik adresi. ID'ler KÜÇÜKTEN BÜYÜĞE
  // sıralanıyor — aksi halde aynı içerik "29-vs-603" ve "603-vs-29"
  // adreslerinden iki kez erişilebilir olurdu (marka karşılaştırma
  // sayfalarındaki alfabetik sıralamayla aynı gerekçe).
  static pairSlug(idA: number, idB: number): string {
    const [first, second] = [idA, idB].sort((a, b) => a - b);
    return `${first}-vs-${second}`;
  }

  comparisonUrl(): string | null {
    const current = this.slots();
    if (current.length < MAX_SLOTS) return null;
    return `/karsilastir-urun/${ComparisonService.pairSlug(current[0].productId, current[1].productId)}`;
  }

  private write(slots: ComparisonSlot[]): void {
    this.slots.set(slots);
    if (!this.isBrowser) return;

    try {
      if (slots.length === 0) localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, JSON.stringify(slots));
    } catch {
      // Depolama kotası dolu ya da kapalı olabilir — seçim yine de bu
      // oturum boyunca signal'de yaşamaya devam etsin, hata fırlatmayalım.
    }
  }

  private readFromStorage(): ComparisonSlot[] {
    if (!this.isBrowser) return [];

    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];

      const parsed = JSON.parse(raw) as ComparisonSlot[];
      // Bozuk/eski biçimli veriye karşı: yalnızca beklenen alanları taşıyan
      // kayıtları al ve en fazla iki tanesini tut.
      return Array.isArray(parsed)
        ? parsed.filter((s) => typeof s?.productId === 'number' && typeof s?.productName === 'string').slice(0, MAX_SLOTS)
        : [];
    } catch {
      return [];
    }
  }
}

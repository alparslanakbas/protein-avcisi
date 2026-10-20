import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, output, signal } from '@angular/core';

import { YonetimOncelikliUrun, YonetimService, YonetimUrun } from './yonetim.service';

/**
 * Elle veri girişinin iş sırası (20 Ekim). ~9.500 eksik alanın çoğu kimsenin açmadığı
 * ürünlerde; ziyaretçinin tıkladığı ürünler önce gelir. Ayrı bileşen, çünkü yönetim
 * sayfasının dosyaları boyut tavanında (scripts/boyut-siniri.sh). Düzenleme sayfanın
 * mevcut düzenleyicisinde açılıyor; bu bileşen yalnız listeyi tutuyor.
 */
@Component({
  selector: 'app-oncelikli-eksikler',
  template: `
    <section
      class="mb-5 rounded-[14px] border border-[var(--noc-divider)] bg-[var(--noc-card)] p-4"
      aria-labelledby="oncelikli-eksikler-baslik"
    >
      <div class="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="oncelikli-eksikler-baslik" class="text-[15px] font-medium">Öncelikli eksikler</h2>
        <button
          type="button"
          class="text-xs text-[var(--noc-accent)] underline underline-offset-2"
          [disabled]="yukleniyor()"
          (click)="yukle()"
        >
          Yenile
        </button>
      </div>
      <p class="mt-1 text-xs leading-relaxed text-[var(--noc-neutral-500)]">
        Ziyaretçilerin mağazaya gitmek için tıkladığı, kategorisi ya da besin değeri eksik ürünler; en çok tıklanan en
        üstte. Kaydettikten sonra Yenile ile liste güncellenir.
        @if (toplam() > urunler().length) {
          Toplam {{ toplam() }} ürün, ilk {{ urunler().length }} tanesi gösteriliyor.
        }
      </p>

      @if (hata(); as h) {
        <p class="mt-3 text-xs text-[var(--pa-danger)]">{{ h }}</p>
      } @else if (yukleniyor() && urunler().length === 0) {
        <p class="mt-3 text-xs text-[var(--noc-neutral-500)]">Yükleniyor…</p>
      } @else if (yuklendi() && urunler().length === 0) {
        <p class="mt-3 text-xs text-[var(--noc-neutral-500)]">Tıklanan ürünlerde eksik veri kalmadı.</p>
      } @else {
        <ol class="mt-3 divide-y divide-[var(--noc-divider)]">
          @for (u of urunler(); track u.id) {
            <li class="flex flex-wrap items-center gap-x-3 gap-y-1 py-2">
              <span
                class="w-10 shrink-0 text-right text-xs tabular-nums text-[var(--noc-neutral-500)]"
                title="Mağazaya gitme tıklaması"
                >{{ u.tiklanma }}</span
              >
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm">{{ u.name }}</span>
                <span class="block text-xs text-[var(--noc-neutral-500)]">{{
                  u.seller ? u.marka + ', satıcı: ' + u.seller : u.marka
                }}</span>
              </span>
              @for (e of eksikler(u); track e) {
                <span class="rounded-full border border-[var(--noc-divider)] px-2 py-0.5 text-[11px]">{{ e }} eksik</span>
              }
              <button
                type="button"
                class="rounded-[8px] border border-[var(--noc-divider)] px-3 py-1 text-xs hover:border-[var(--noc-accent)]"
                (click)="duzenle.emit(u)"
              >
                Düzenle
              </button>
            </li>
          }
        </ol>
      }
    </section>
  `,
})
export class YonetimOncelikliEksikler implements OnInit {
  private readonly api = inject(YonetimService);
  private readonly tarayici = isPlatformBrowser(inject(PLATFORM_ID));

  /** Sayfanın veri düzenleyicisini açar (YonetimPage.veriDuzenleyiciAc). */
  readonly duzenle = output<YonetimUrun>();

  readonly urunler = signal<YonetimOncelikliUrun[]>([]);
  readonly toplam = signal(0);
  readonly yukleniyor = signal(false);
  readonly yuklendi = signal(false);
  readonly hata = signal<string | null>(null);

  ngOnInit(): void {
    // Panel istemcide çiziliyor (RenderMode.Client); sunucuda istek atılmasın.
    if (this.tarayici) this.yukle();
  }

  yukle(): void {
    this.yukleniyor.set(true);
    this.hata.set(null);
    this.api.oncelikliEksikler().subscribe({
      next: (sonuc) => {
        this.urunler.set(sonuc.urunler);
        this.toplam.set(sonuc.toplam);
        this.yukleniyor.set(false);
        this.yuklendi.set(true);
      },
      error: () => {
        this.yukleniyor.set(false);
        this.hata.set('Öncelikli eksikler yüklenemedi.');
      },
    });
  }

  /** Eksik alanlar; "tablosu yok" diye kapatılan besin eksik sayılmaz (backend ile aynı kural). */
  eksikler(u: YonetimUrun): string[] {
    const sonuc: string[] = [];
    if (!u.category) sonuc.push('Kategori');
    if (!u.nutritionJson && !u.nutritionIsManual) sonuc.push('Besin');
    return sonuc;
  }
}

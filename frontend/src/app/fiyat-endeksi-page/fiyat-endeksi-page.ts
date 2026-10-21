import { DOCUMENT, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { API_BASE_URL } from '../core/api.config';
import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { canonicalOrigin } from '../core/canonical-link';
import { DealsService } from '../core/deals.service';
import {
  FiyatEndeksi,
  FiyatEndeksiAyi,
  TUM_KATEGORILER,
  ayAdi,
  degisimMetni,
  kategoriAdi,
  oncekiAyaGore,
} from '../core/fiyat-endeksi';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

/**
 * Aylık takviye fiyat endeksi. Hesap backend'de (FiyatEndeksiServisi): aynı ürünlerin ay içindeki tipik fiyatı bir
 * önceki aya göre, geometrik ortalama, Eylül 2026 = 100. Sayfa tamamen veriden kuruluyor, her ay elle yazılacak bir
 * metin yok (Ağustos'ta endeks bu yüzden ertelenmişti).
 *
 * Google Dataset Search için Dataset işaretlemesi ve CSV (CC BY 4.0): gazeteci ve araştırmacının bulacağı yer orası.
 */
@Component({
  selector: 'app-fiyat-endeksi-page',
  imports: [DecimalPipe, RouterLink, SiteHeader],
  templateUrl: './fiyat-endeksi-page.html',
})
export class FiyatEndeksiPage implements OnInit {
  private readonly dealsService = inject(DealsService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);

  protected readonly endeks = signal<FiyatEndeksi | null>(null);
  protected readonly yukleniyor = signal(true);
  protected readonly hata = signal(false);

  protected readonly sonAy = computed<FiyatEndeksiAyi | null>(() => this.endeks()?.aylar[0] ?? null);
  protected readonly tum = computed(() => this.sonAy()?.kategoriler.find((k) => k.kategori === TUM_KATEGORILER) ?? null);
  protected readonly protein = computed(() => this.sonAy()?.kategoriler.find((k) => k.kategori === 'protein-tozu') ?? null);

  /** Geçmiş tablosu: her ayın bütün kategoriler ve protein tozu endeksi, en altta taban ay. */
  protected readonly gecmis = computed(() =>
    (this.endeks()?.aylar ?? []).map((a) => ({
      ay: a.ay,
      gecici: a.gecici,
      tum: a.kategoriler.find((k) => k.kategori === TUM_KATEGORILER)?.endeks ?? null,
      protein: a.kategoriler.find((k) => k.kategori === 'protein-tozu')?.endeks ?? null,
    })),
  );

  protected readonly csvAdresi = `${API_BASE_URL}/api/fiyat-endeksi.csv`;
  protected readonly ayAdi = ayAdi;
  protected readonly oncekiAyaGore = oncekiAyaGore;
  protected readonly degisimMetni = degisimMetni;
  protected readonly kategoriAdi = kategoriAdi;

  ngOnInit(): void {
    this.metaYaz(null);
    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Fiyat Endeksi', path: '/fiyat-endeksi' },
      ]),
    );

    this.dealsService.getFiyatEndeksi().subscribe({
      next: (endeks) => {
        this.endeks.set(endeks);
        this.yukleniyor.set(false);
        this.metaYaz(endeks.aylar[0] ?? null);
        upsertJsonLdScript(this.document, null, this.dataset(endeks));
      },
      error: () => {
        this.hata.set(true);
        this.yukleniyor.set(false);
      },
    });
  }

  protected tarih(iso: string): string {
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Europe/Istanbul' }).format(
      new Date(iso),
    );
  }

  private metaYaz(ay: FiyatEndeksiAyi | null): void {
    const protein = ay?.kategoriler.find((k) => k.kategori === 'protein-tozu');
    this.pageMeta.set({
      title: ay ? `Protein Tozu Fiyat Endeksi: ${ayAdi(ay.ay)} | Protein Avcısı` : 'Takviye Fiyat Endeksi | Protein Avcısı',
      description:
        ay && protein
          ? `${ayAdi(ay.ay)}: protein tozu fiyatları ${oncekiAyaGore(ay.ay)} göre ${degisimMetni(protein.aylikDegisimYuzde)}. ` +
            'Aynı ürünlerin aylık fiyat değişimi, kategori kategori; yöntem ve indirilebilir veri.'
          : 'Takviye fiyatları ay ay ne kadar değişti? Aynı ürünlerin aylık fiyat değişimi, kategori kategori; yöntem ve indirilebilir veri.',
      canonicalPath: '/fiyat-endeksi',
    });
  }

  private dataset(endeks: FiyatEndeksi): object {
    const origin = canonicalOrigin(this.document);
    const son = endeks.aylar[0]?.ay.slice(0, 7) ?? endeks.tabanAy.slice(0, 7);
    return {
      '@context': 'https://schema.org',
      '@type': 'Dataset',
      name: 'Protein Avcısı Takviye Fiyat Endeksi',
      description:
        "Türkiye'de satılan spor takviyelerinin aylık fiyat değişimi: aynı ürünlerin ay içindeki tipik fiyatının bir " +
        'önceki aya oranı, geometrik ortalama (Jevons), kategori kategori. Taban Eylül 2026 = 100.',
      url: `${origin}/fiyat-endeksi`,
      license: 'https://creativecommons.org/licenses/by/4.0/',
      isAccessibleForFree: true,
      creator: { '@type': 'Organization', name: 'Protein Avcısı', url: origin },
      temporalCoverage: `${endeks.tabanAy.slice(0, 7)}/${son}`,
      spatialCoverage: { '@type': 'Place', name: 'Türkiye' },
      variableMeasured: ['Fiyat endeksi (Eylül 2026 = 100)', 'Aylık fiyat değişimi (%)'],
      distribution: [{ '@type': 'DataDownload', encodingFormat: 'text/csv', contentUrl: this.csvAdresi }],
    };
  }
}

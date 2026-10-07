import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { FaqItem } from '../core/category-faqs';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { GoalPickSection } from '../core/supplement-goals';
import { SiteHeader } from '../site-header/site-header';
import { ValuePickList } from '../value-pick-list/value-pick-list';

const SAYFA_YOLU = '/en-ucuz-kreatin';

// Sıralama ve kapsam backend'de tek yerde: ValuePickRanker'ın kreatin kuralı (100 g - 2 kg, karışımlar ve çok ürünlü
// paketler dışarıda, her markadan bir ürün). "Hangi takviye?" sayfaları aynı listeyi 6 ürünle gösteriyor.
const KREATIN_LISTESI: GoalPickSection = {
  id: 'siralama',
  category: 'kreatin',
  title: 'Kilogram fiyatına göre sıralama',
  allLink: { label: 'Tüm kreatin fiyatları', path: '/kategori/kreatin' },
};

// Fiyat soruları. Kreatinin kendisiyle ilgili sorular (saç döker mi, ara verilir mi) kategori sayfasında; burada
// tekrarlansalar iki sayfa aynı aramalarla yarışırdı.
const SORULAR: FaqItem[] = [
  {
    question: 'Büyük paket her zaman daha mı ucuz?',
    answer:
      'Her zaman değil. Kampanyadaki küçük bir paket, indirimsiz büyük paketten kilogram başına daha ucuza gelebiliyor. ' +
      'Bu yüzden sıralamada paket boyutuna değil kilogram fiyatına bakıyoruz.',
  },
  {
    question: 'Günlük maliyet ne kadar tutar?',
    answer:
      'Günde 3-5 gram, kreatin için en yaygın kullanılan aralık. Günde 5 gramla 1 kilogram 200 gün yeter; kilogram ' +
      'fiyatını 200’e bölersen yaklaşık günlük maliyeti bulursun. Kilogramı 1.000 TL olan bir kreatin günde 5 TL tutar.',
  },
  {
    question: 'Fiyatlar ne sıklıkla güncelleniyor?',
    answer:
      'Mağazaların çoğunu 6 saatte bir, bazı bayileri günde bir ya da iki kez tarıyoruz; sıralama her taramadan sonra ' +
      'yeniden hesaplanıyor. Sepette uygulanan kupon kodları fiyata dahil değil.',
  },
  {
    question: 'Yeşil etiketler ne anlama geliyor?',
    answer:
      'İkisi de bizim fiyat geçmişimize dayanıyor, mağazanın üstü çizili fiyatına değil. "Gerçek indirim", bugünkü ' +
      'fiyatın son 30 günde en az 7 farklı günde görülen fiyatın altında olduğunu gösteriyor. "… beri en düşük" ise ' +
      'fiyatı takip etmeye başladığımız günden beri gördüğümüz en düşük fiyat.',
  },
];

/**
 * Kalıcı liste sayfası: kilogram fiyatına göre en ucuz kreatin. Gram protein başına liste bilerek yok: protein
 * tozlarının yalnızca %14'ünde porsiyon ve protein verisi var (7 Ekim), kreatinin ise %79'unda paket ağırlığı biliniyor.
 */
@Component({
  selector: 'app-en-ucuz-kreatin-page',
  imports: [RouterLink, SiteHeader, ValuePickList],
  templateUrl: './en-ucuz-kreatin-page.html',
})
export class EnUcuzKreatinPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);

  protected readonly liste = KREATIN_LISTESI;
  protected readonly sorular = SORULAR;

  ngOnInit(): void {
    const yil = new Date().getFullYear();
    this.pageMeta.set({
      title: `En Ucuz Kreatin ${yil}: Kg Fiyatına Göre | Protein Avcısı`,
      description:
        `Kreatin fiyatları kilogram başına: ${yil} güncel en ucuz kreatinler, her markadan en uygun paket. ` +
        'Fiyatlar her gün mağazalardan güncelleniyor.',
      canonicalPath: SAYFA_YOLU,
    });
    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Kreatin', path: '/kategori/kreatin' },
        { name: 'En ucuz kreatin', path: SAYFA_YOLU },
      ]),
    );
    upsertJsonLdScript(this.document, null, {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: SORULAR.map((s) => ({
        '@type': 'Question',
        name: s.question,
        acceptedAnswer: { '@type': 'Answer', text: s.answer },
      })),
    });
  }
}

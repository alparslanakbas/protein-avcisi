import { Component, OnInit, PLATFORM_ID, RESPONSE_INIT, inject } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { RouterLink } from '@angular/router';

import { PageMetaService } from '../core/page-meta.service';
import { SITE_NAME } from '../core/site-identity';
import { SiteHeader } from '../site-header/site-header';

/**
 * Bulunamayan sayfa (404).
 *
 * NEDEN VAR: 4 Eylül'e kadar rota listesinde yakalayıcı (`**`) rota YOKTU.
 * Angular router adresi eşleştiremeyince istek SSR katmanına düşüyor ve
 * Express'in çıplak varsayılanı basılıyordu:
 *
 *     <title>Error</title>
 *     <pre>Cannot GET /olmayan-sayfa</pre>
 *
 * 1 kB'lık, sitenin hiçbir öğesini taşımayan bir hata metni: başlık yok,
 * menü yok, kullanıcının geri dönebileceği hiçbir bağlantı yok. Google'ın ve
 * AdSense denetçisinin gördüğü sayfa da buydu.
 *
 * <b>DURUM KODU 404 OLMAK ZORUNDA.</b> Bileşen sunucuda `RESPONSE_INIT` ile
 * kodu 404'e çekiyor. Yapılmasaydı sayfa 200 dönerdi ve Google bunu "soft
 * 404" sayardı — yani var olmayan içerik için geçerli sayfa sinyali. Aynı
 * mekanizma inceleme sayfasında 503 için de kullanılıyor.
 *
 * Ayrıca `noIndex`: 404 zaten dizine girmez ama tek sayfa uygulamasında
 * kullanıcı buraya istemcide de gelebiliyor (o durumda HTTP kodu yok),
 * etiket o yolu da kapatıyor.
 */
@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './not-found-page.html',
  styleUrl: './not-found-page.css',
})
export class NotFoundPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly isServer = isPlatformServer(inject(PLATFORM_ID));

  ngOnInit(): void {
    if (this.isServer && this.responseInit) {
      this.responseInit.status = 404;
    }

    this.pageMeta.set({
      title: `Sayfa bulunamadı | ${SITE_NAME}`,
      description:
        'Aradığın sayfa bulunamadı. Kategorilerden, markalardan ya da arama kutusundan devam edebilirsin.',
      // Kanonik olarak KENDİ adresi verilmiyor: bu sayfa birçok farklı
      // adreste görünüyor ve her birini ayrı bir kanonik sayfa ilan etmek
      // dizine kopya adres bildirmek olurdu.
      canonicalPath: '/',
      noIndex: true,
    });
  }
}

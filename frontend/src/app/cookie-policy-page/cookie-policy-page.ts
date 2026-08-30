import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-cookie-policy-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './cookie-policy-page.html',
  styleUrl: './cookie-policy-page.css',
})
export class CookiePolicyPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Çerez Politikası | ProteinAvcısı',
      description: 'ProteinAvcısı hangi çerezleri/yerel depolama teknolojilerini kullanıyor, ne zaman izin isteyeceğiz — açıkça anlatıyoruz.',
      canonicalPath: '/cerez-politikasi',
    });
  }
}

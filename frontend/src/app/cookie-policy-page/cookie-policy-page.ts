import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PageMetaService } from '../core/page-meta.service';

@Component({
  selector: 'app-cookie-policy-page',
  imports: [RouterLink],
  templateUrl: './cookie-policy-page.html',
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

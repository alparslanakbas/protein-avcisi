import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-privacy-policy-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './privacy-policy-page.html',
})
export class PrivacyPolicyPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Gizlilik Politikası | ProteinAvcısı',
      description: 'ProteinAvcısı hangi verileri topluyor, nasıl kullanıyor ve KVKK kapsamındaki haklarınız neler — açıkça anlatıyoruz.',
      canonicalPath: '/gizlilik-politikasi',
    });
  }
}

import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { setCanonicalLink } from '../core/canonical-link';

@Component({
  selector: 'app-cookie-policy-page',
  imports: [RouterLink],
  templateUrl: './cookie-policy-page.html',
})
export class CookiePolicyPage implements OnInit {
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  ngOnInit(): void {
    const title = 'Çerez Politikası | ProteinAvcısı';
    const description = 'ProteinAvcısı hangi çerezleri/yerel depolama teknolojilerini kullanıyor, ne zaman izin isteyeceğiz — açıkça anlatıyoruz.';

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    setCanonicalLink(this.document, '/cerez-politikasi');
  }
}

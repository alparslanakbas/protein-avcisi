import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { setCanonicalLink } from '../core/canonical-link';

@Component({
  selector: 'app-privacy-policy-page',
  imports: [RouterLink],
  templateUrl: './privacy-policy-page.html',
})
export class PrivacyPolicyPage implements OnInit {
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  ngOnInit(): void {
    const title = 'Gizlilik Politikası | ProteinAvcısı';
    const description = 'ProteinAvcısı hangi verileri topluyor, nasıl kullanıyor ve KVKK kapsamındaki haklarınız neler — açıkça anlatıyoruz.';

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    setCanonicalLink(this.document, '/gizlilik-politikasi');
  }
}

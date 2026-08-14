import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { setCanonicalLink } from '../core/canonical-link';

@Component({
  selector: 'app-how-it-works-page',
  imports: [RouterLink],
  templateUrl: './how-it-works-page.html',
})
export class HowItWorksPage implements OnInit {
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  ngOnInit(): void {
    const title = 'Nasıl Çalışıyoruz? | ProteinAvcısı';
    const description = 'ProteinAvcısı fiyatları nasıl topluyor, "gerçek indirim" nasıl hesaplanıyor, gelir modelimiz sıralamayı etkiliyor mu — açıkça anlatıyoruz.';

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    setCanonicalLink(this.document, '/nasil-calisiyoruz');
  }
}

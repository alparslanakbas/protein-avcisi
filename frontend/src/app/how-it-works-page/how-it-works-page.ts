import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-how-it-works-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './how-it-works-page.html',
  styleUrl: './how-it-works-page.css',
})
export class HowItWorksPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Nasıl Çalışıyoruz? | ProteinAvcısı',
      description: 'ProteinAvcısı fiyatları nasıl topluyor, "gerçek indirim" nasıl hesaplanıyor, gelir modelimiz sıralamayı etkiliyor mu — açıkça anlatıyoruz.',
      canonicalPath: '/nasil-calisiyoruz',
    });
  }
}

import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { FOUNDER, SITE_NAME } from '../core/site-identity';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-contact-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './contact-page.html',
  styleUrl: './contact-page.css',
})
export class ContactPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);

  protected readonly founder = FOUNDER;

  ngOnInit(): void {
    this.pageMeta.set({
      title: `İletişim | ${SITE_NAME}`,
      description: `${SITE_NAME} ile iletişime geç: yanlış fiyat bildirimi, marka iş birliği, reklam ve basın talepleri için doğrudan e-posta adresi.`,
      canonicalPath: '/iletisim',
    });

    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'İletişim', path: '/iletisim' },
      ]),
    );
  }
}

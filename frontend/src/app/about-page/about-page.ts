import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { FOUNDER, SITE_NAME } from '../core/site-identity';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-about-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './about-page.html',
  styleUrl: './about-page.css',
})
export class AboutPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);

  protected readonly founder = FOUNDER;

  ngOnInit(): void {
    this.pageMeta.set({
      title: `Hakkımızda | ${SITE_NAME}`,
      description: `${SITE_NAME}'yı kim, neden kurdu? Kurucu ${FOUNDER.name} ve platformun gerçek fiyat geçmişine dayanan yaklaşımı hakkında.`,
      canonicalPath: '/hakkimizda',
    });

    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Hakkımızda', path: '/hakkimizda' },
      ]),
    );
  }
}

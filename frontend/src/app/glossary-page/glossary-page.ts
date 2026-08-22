import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { GLOSSARY } from '../core/glossary';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { SITE_NAME } from '../core/site-identity';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-glossary-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './glossary-page.html',
})
export class GlossaryPage implements OnInit {
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);

  protected readonly groups = GLOSSARY;

  ngOnInit(): void {
    this.pageMeta.set({
      title: `Spor Takviyesi Sözlüğü | ${SITE_NAME}`,
      description:
        'BCAA, EAA, kreatin monohidrat, whey izole, biyoyararlanım gibi spor takviyesi terimlerinin kısa ve dürüst tanımları.',
      canonicalPath: '/sozluk',
    });

    upsertJsonLdScript(
      this.document,
      null,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Sözlük', path: '/sozluk' },
      ]),
    );

    // DefinedTermSet — Google'ın sözlük/tanım sayfaları için beklediği
    // structured data tipi.
    upsertJsonLdScript(this.document, null, {
      '@context': 'https://schema.org',
      '@type': 'DefinedTermSet',
      name: `${SITE_NAME} Spor Takviyesi Sözlüğü`,
      hasDefinedTerm: this.groups.flatMap((group) =>
        group.terms.map((t) => ({
          '@type': 'DefinedTerm',
          name: t.term,
          description: t.definition,
        })),
      ),
    });
  }
}

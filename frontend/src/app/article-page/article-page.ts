import { DOCUMENT, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Article } from '../core/article.model';
import { ArticlesService } from '../core/articles.service';
import { canonicalOrigin } from '../core/canonical-link';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';

@Component({
  selector: 'app-article-page',
  imports: [RouterLink, DatePipe],
  templateUrl: './article-page.html',
})
export class ArticlePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly articlesService = inject(ArticlesService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private structuredDataEl: HTMLScriptElement | null = null;

  protected readonly article = signal<Article | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug') ?? '';
      this.loadArticle(slug);
    });
  }

  private loadArticle(slug: string): void {
    this.loading.set(true);

    this.articlesService.getArticleBySlug(slug).subscribe({
      next: (article) => {
        this.article.set(article);
        this.setMeta(article);
        this.loading.set(false);
      },
      error: () => this.router.navigate(['/rehber']),
    });
  }

  private setMeta(article: Article): void {
    const title = `${article.title} | ProteinAvcısı Rehber`;

    this.pageMeta.set({
      title,
      description: article.summary,
      canonicalPath: `/rehber/${article.slug}`,
      ogType: 'article',
      ogImage: article.coverImageUrl ?? undefined,
    });

    const jsonLd = {
      '@context': 'https://schema.org',
      '@type': 'Article',
      headline: article.title,
      description: article.summary,
      datePublished: article.publishedAt,
      ...(article.coverImageUrl ? { image: article.coverImageUrl } : {}),
      author: { '@type': 'Organization', name: 'ProteinAvcısı' },
      mainEntityOfPage: `${canonicalOrigin(this.document)}/rehber/${article.slug}`,
    };

    this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, jsonLd);
  }
}

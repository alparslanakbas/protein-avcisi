import { DOCUMENT, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Article } from '../core/article.model';
import { ArticlesService } from '../core/articles.service';
import { canonicalOrigin, setCanonicalLink } from '../core/canonical-link';

@Component({
  selector: 'app-article-page',
  imports: [RouterLink, DatePipe],
  templateUrl: './article-page.html',
})
export class ArticlePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly articlesService = inject(ArticlesService);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
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

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: article.summary });
    this.metaService.updateTag({ property: 'og:title', content: title });
    this.metaService.updateTag({ property: 'og:description', content: article.summary });
    this.metaService.updateTag({ property: 'og:type', content: 'article' });
    if (article.coverImageUrl) {
      this.metaService.updateTag({ property: 'og:image', content: article.coverImageUrl });
    } else {
      this.metaService.removeTag('property="og:image"');
    }
    setCanonicalLink(this.document, `/rehber/${article.slug}`);

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

    if (!this.structuredDataEl) {
      this.structuredDataEl = this.document.createElement('script');
      this.structuredDataEl.type = 'application/ld+json';
      this.document.head.appendChild(this.structuredDataEl);
    }
    this.structuredDataEl.textContent = JSON.stringify(jsonLd);
  }
}

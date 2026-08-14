import { DOCUMENT } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { ArticleSummary } from '../core/article.model';
import { ArticlesService } from '../core/articles.service';
import { setCanonicalLink } from '../core/canonical-link';

@Component({
  selector: 'app-article-list-page',
  imports: [RouterLink],
  templateUrl: './article-list-page.html',
})
export class ArticleListPage implements OnInit {
  private readonly articlesService = inject(ArticlesService);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);
  private readonly document = inject(DOCUMENT);

  protected readonly articles = signal<ArticleSummary[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const title = 'Rehber | ProteinAvcısı';
    const description = 'Protein tozu, kreatin, pre-workout ve diğer spor takviyeleri hakkında bilgi amaçlı rehberler — hangi ürünü nasıl seçeceğine dair gerçek, tarafsız içerik.';

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    setCanonicalLink(this.document, '/rehber');

    this.articlesService.getArticles().subscribe({
      next: (articles) => {
        this.articles.set(articles);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}

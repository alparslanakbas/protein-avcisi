import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ArticleSummary } from '../core/article.model';
import { ArticlesService } from '../core/articles.service';
import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

@Component({
  selector: 'app-article-list-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './article-list-page.html',
})
export class ArticleListPage implements OnInit {
  private readonly articlesService = inject(ArticlesService);
  private readonly pageMeta = inject(PageMetaService);

  protected readonly articles = signal<ArticleSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Rehber | ProteinAvcısı',
      description: 'Protein tozu, kreatin, pre-workout ve diğer spor takviyeleri hakkında bilgi amaçlı rehberler — hangi ürünü nasıl seçeceğine dair gerçek, tarafsız içerik.',
      canonicalPath: '/rehber',
    });

    this.articlesService.getArticles().subscribe({
      next: (articles) => {
        this.articles.set(articles);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }
}

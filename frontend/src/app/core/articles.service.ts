import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Article, ArticleSummary } from './article.model';

@Injectable({ providedIn: 'root' })
export class ArticlesService {
  private readonly http = inject(HttpClient);

  getArticles(): Observable<ArticleSummary[]> {
    return this.http.get<ArticleSummary[]>(`${API_BASE_URL}/api/articles`);
  }

  getArticleBySlug(slug: string): Observable<Article> {
    return this.http.get<Article>(`${API_BASE_URL}/api/articles/${slug}`);
  }
}

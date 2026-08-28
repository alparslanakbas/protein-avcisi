import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ArticleSummary } from '../core/article.model';
import { ArticlesService } from '../core/articles.service';
import { PageMetaService } from '../core/page-meta.service';
import { SiteHeader } from '../site-header/site-header';

type GuideTone = 'violet' | 'blue' | 'mint' | 'rose' | 'orange';

interface LearningPathConfig {
  title: string;
  description: string;
  iconClass: string;
  tone: GuideTone;
  articleSlugs: string[];
}

interface LearningPath extends LearningPathConfig {
  articles: ArticleSummary[];
}

const LEARNING_PATH_CONFIGS: LearningPathConfig[] = [
  {
    title: 'Yeni başlıyorum',
    description: 'Temelleri öğren, doğru ürünleri seç.',
    iconClass: 'ph-star',
    tone: 'violet',
    articleSlugs: ['whey-protein-nasil-secilir', 'kreatin-nasil-kullanilir'],
  },
  {
    title: 'Performansımı artırmak istiyorum',
    description: 'Antrenman performansını destekle.',
    iconClass: 'ph-lightning',
    tone: 'blue',
    articleSlugs: ['pre-workout-nasil-secilir', 'bcaa-mi-eaa-mi-amino-asit-rehberi'],
  },
  {
    title: 'Kilo kontrolünü anlamak istiyorum',
    description: 'Yağ yönetimi ve metabolizma desteği.',
    iconClass: 'ph-fire',
    tone: 'mint',
    articleSlugs: ['l-karnitin-yag-yakiminda-ise-yarar-mi', 'yag-yakici-takviyeler-gercekten-ise-yarar-mi'],
  },
];

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
  protected readonly searchQuery = signal('');

  protected readonly hasSearch = computed(() => this.searchQuery().trim().length > 0);
  protected readonly featuredArticles = computed(() => this.articles().slice(0, 3));
  protected readonly topicColumns = computed(() => {
    const remaining = this.articles().slice(3);
    const middle = Math.ceil(remaining.length / 2);
    return [remaining.slice(0, middle), remaining.slice(middle)];
  });
  protected readonly filteredArticles = computed(() => {
    const query = this.normalizeSearch(this.searchQuery());
    if (!query) return this.articles();

    return this.articles().filter((article) =>
      this.normalizeSearch(`${article.title} ${article.summary} ${article.slug}`).includes(query),
    );
  });
  protected readonly learningPaths = computed<LearningPath[]>(() => {
    const articlesBySlug = new Map(this.articles().map((article) => [article.slug, article]));

    return LEARNING_PATH_CONFIGS.map((path) => ({
      ...path,
      articles: path.articleSlugs
        .map((slug) => articlesBySlug.get(slug))
        .filter((article): article is ArticleSummary => article !== undefined),
    })).filter((path) => path.articles.length > 0);
  });

  ngOnInit(): void {
    this.pageMeta.set({
      // "Rehber" tek başına hiçbir arama niyetiyle eşleşmiyordu — konuyu
      // (spor takviyesi) taşıyan bir başlık hem title hem H1'de kullanılıyor.
      title: 'Spor Takviyesi Rehberi | ProteinAvcısı',
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

  protected setSearchQuery(value: string): void {
    this.searchQuery.set(value);
  }

  protected clearSearch(): void {
    this.searchQuery.set('');
  }

  protected formatArticleDate(value: string): string {
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'Europe/Istanbul',
    }).format(new Date(value));
  }

  protected articleIconClass(article: ArticleSummary): string {
    const slug = article.slug;
    if (slug.includes('kreatin-ve-protein')) return 'ph-file-text';
    if (slug.includes('pre-workout')) return 'ph-lightning';
    if (slug.includes('protein') || slug.includes('whey')) return 'ph-jar';
    if (slug.includes('kreatin')) return 'ph-barbell';
    if (slug.includes('vitamin')) return 'ph-shield-plus';
    if (slug.includes('atistirmalik')) return 'ph-cookie';
    if (slug.includes('bcaa') || slug.includes('eaa')) return 'ph-share-network';
    if (slug.includes('gainer')) return 'ph-barbell';
    if (slug.includes('yag') || slug.includes('karnitin')) return 'ph-fire';
    return 'ph-book-open-text';
  }

  protected articleTone(article: ArticleSummary): GuideTone {
    const slug = article.slug;
    if (slug.includes('kreatin-ve-protein')) return 'violet';
    if (slug.includes('pre-workout')) return 'rose';
    if (slug.includes('protein') || slug.includes('whey')) return 'mint';
    if (slug.includes('vitamin') || slug.includes('atistirmalik')) return 'orange';
    if (slug.includes('yag') || slug.includes('karnitin')) return 'mint';
    if (slug.includes('bcaa') || slug.includes('eaa')) return 'blue';
    return 'violet';
  }

  private normalizeSearch(value: string): string {
    return value
      .toLocaleLowerCase('tr-TR')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .trim();
  }
}

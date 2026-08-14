export interface ArticleSummary {
  id: number;
  title: string;
  slug: string;
  summary: string;
  coverImageUrl: string | null;
  publishedAt: string;
}

export interface Article extends ArticleSummary {
  body: string;
}

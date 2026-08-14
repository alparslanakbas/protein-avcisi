namespace IndirimTakip.Infrastructure.Articles;

// Liste görünümünde Body (uzun HTML) gereksiz — sadece detay sayfasında lazım.
public record ArticleSummaryDto(int Id, string Title, string Slug, string Summary, string? CoverImageUrl, DateTimeOffset PublishedAt);

public record ArticleDto(int Id, string Title, string Slug, string Summary, string Body, string? CoverImageUrl, DateTimeOffset PublishedAt);

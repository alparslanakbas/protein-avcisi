using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Articles;

public record CreateArticleRequest(string Title, string Slug, string Summary, string Body, string? CoverImageUrl);

public class ArticleService(AppDbContext db)
{
    public async Task<IReadOnlyList<ArticleSummaryDto>> GetPublishedArticlesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Articles
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.PublishedAt)
            .Select(a => new ArticleSummaryDto(a.Id, a.Title, a.Slug, a.Summary, a.CoverImageUrl, a.PublishedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ArticleDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Slug == slug && a.IsPublished, cancellationToken);
        if (article is null)
            return null;

        return new ArticleDto(article.Id, article.Title, article.Slug, article.Summary, article.Body, article.CoverImageUrl, article.PublishedAt);
    }

    public async Task<ArticleDto?> CreateAsync(CreateArticleRequest request, CancellationToken cancellationToken = default)
    {
        var slugTaken = await db.Articles.AnyAsync(a => a.Slug == request.Slug, cancellationToken);
        if (slugTaken)
            return null;

        var article = new Article
        {
            Title = request.Title,
            Slug = request.Slug,
            Summary = request.Summary,
            Body = request.Body,
            CoverImageUrl = request.CoverImageUrl,
            PublishedAt = DateTimeOffset.UtcNow,
            IsPublished = true,
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync(cancellationToken);

        return new ArticleDto(article.Id, article.Title, article.Slug, article.Summary, article.Body, article.CoverImageUrl, article.PublishedAt);
    }
}

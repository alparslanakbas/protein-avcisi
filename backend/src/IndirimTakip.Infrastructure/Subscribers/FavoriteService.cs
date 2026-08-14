using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

public record FavoriteRequest(string? Token, string? Email);

// Hesap/login gerektirmeyen "favorilerim" listesi — Subscriber'ın e-posta+
// token altyapısını (Haber Ver ile aynı) yeniden kullanıyor ama hiç
// e-posta göndermiyor, bu yüzden onay akışına (IsConfirmed) hiç girmiyor.
// Token ilk favori eklemede dönüyor, frontend bunu localStorage'da tutup
// sonraki ekleme/kaldırma/listeleme isteklerinde kullanıyor.
public class FavoriteService(AppDbContext db, SubscriberService subscribers)
{
    public async Task<string?> AddAsync(int productId, string? token, string? email, CancellationToken cancellationToken = default)
    {
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
            return null;

        var subscriber = await ResolveSubscriberAsync(token, email, cancellationToken);
        if (subscriber is null)
            return null;

        var alreadyFavorited = await db.ProductFavorites.AnyAsync(
            f => f.SubscriberId == subscriber.Id && f.ProductId == productId, cancellationToken);

        if (!alreadyFavorited)
        {
            db.ProductFavorites.Add(new ProductFavorite
            {
                SubscriberId = subscriber.Id,
                ProductId = productId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return subscriber.Token;
    }

    public async Task<bool> RemoveAsync(int productId, string token, CancellationToken cancellationToken = default)
    {
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
        if (subscriber is null)
            return false;

        var favorite = await db.ProductFavorites.FirstOrDefaultAsync(
            f => f.SubscriberId == subscriber.Id && f.ProductId == productId, cancellationToken);
        if (favorite is null)
            return false;

        db.ProductFavorites.Remove(favorite);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<int>?> GetFavoriteProductIdsAsync(string token, CancellationToken cancellationToken = default)
    {
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
        if (subscriber is null)
            return null;

        return await db.ProductFavorites
            .Where(f => f.SubscriberId == subscriber.Id)
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);
    }

    private async Task<Subscriber?> ResolveSubscriberAsync(string? token, string? email, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(token))
        {
            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
            if (existing is not null)
                return existing;
        }

        return string.IsNullOrWhiteSpace(email) ? null : await subscribers.GetOrCreateSubscriberAsync(email, cancellationToken);
    }
}

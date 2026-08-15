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
    // 2026-08-15 güvenlik denetimi: bu metot önceden var olan bir abonenin
    // Token'ını sadece e-postasını bilerek isteyen herkese döndürüyordu —
    // Token hem onay hem abonelikten çıkma hem favoriler için kullanıldığı
    // için, bu bir kişinin e-postasını bilen başkasının onun bülten aboneliğini
    // (double opt-in atlatarak) onaylamasına/iptal etmesine, favorilerini
    // okuyup değiştirmesine izin veriyordu. Artık Token SADECE bu çağrıda
    // gerçekten YENİ oluşturulan bir abone için dönüyor; e-posta zaten kayıtlı
    // bir aboneye aitse favori yine ekleniyor (Success=true) ama Token null
    // dönüyor — o hesabın token'ına yalnızca zaten sahip olan (localStorage'da
    // tutan) erişebilir.
    public async Task<(bool Success, string? Token)> AddAsync(int productId, string? token, string? email, CancellationToken cancellationToken = default)
    {
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
            return (false, null);

        var (subscriber, isNewSubscriber) = await ResolveSubscriberAsync(token, email, cancellationToken);
        if (subscriber is null)
            return (false, null);

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

        return (true, isNewSubscriber ? subscriber.Token : null);
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

    private async Task<(Subscriber? Subscriber, bool IsNewSubscriber)> ResolveSubscriberAsync(string? token, string? email, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(token))
        {
            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
            if (existing is not null)
                return (existing, false);
        }

        if (string.IsNullOrWhiteSpace(email))
            return (null, false);

        var normalized = email.Trim().ToLowerInvariant();
        var alreadyExisted = await db.Subscribers.AnyAsync(s => s.Email == normalized, cancellationToken);
        var subscriber = await subscribers.GetOrCreateSubscriberAsync(email, cancellationToken);
        return (subscriber, !alreadyExisted);
    }
}

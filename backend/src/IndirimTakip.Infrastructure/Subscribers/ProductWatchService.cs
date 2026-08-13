using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

public record WatchProductRequest(string Email);

// "Haber Ver" isteğini kaydediyor — asıl fiyat düşünce bildirim gönderme
// işi ProductWatchNotifier'da (tarama döngüsünün bir parçası).
public class ProductWatchService(AppDbContext db, SubscriberService subscribers)
{
    public async Task<bool> WatchAsync(int productId, WatchProductRequest request, string confirmBaseUrl, CancellationToken cancellationToken = default)
    {
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
            return false;

        var subscriber = await subscribers.GetOrCreateSubscriberAsync(request.Email, cancellationToken);

        var alreadyWatching = await db.ProductWatches.AnyAsync(
            w => w.SubscriberId == subscriber.Id && w.ProductId == productId && w.NotifiedAt == null,
            cancellationToken);

        if (!alreadyWatching)
        {
            db.ProductWatches.Add(new ProductWatch
            {
                SubscriberId = subscriber.Id,
                ProductId = productId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Genel bülten onayı aynı zamanda "Haber Ver" bildirimleri için de
        // izin niteliğinde — ayrı bir onay akışı kurmak bu hafif özellik
        // için gereksiz olurdu. Zaten onaylıysa yeni bir mail gitmiyor.
        if (!subscriber.IsConfirmed)
            await subscribers.SendConfirmationEmailAsync(subscriber, confirmBaseUrl, cancellationToken);

        return true;
    }
}

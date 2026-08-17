using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

public record FavoriteRequest(string? Token, string? Email);

// Hesap/login gerektirmeyen "favorilerim" listesi — Subscriber'ın e-posta+
// token altyapısını (Haber Ver ile aynı) yeniden kullanıyor ama hiç
// e-posta göndermiyor, bu yüzden onay akışına (IsConfirmed) hiç girmiyor.
// Token ilk favori eklemede dönüyor, frontend bunu localStorage'da tutup
// sonraki ekleme/kaldırma/listeleme isteklerinde kullanıyor.
public class FavoriteService(AppDbContext db, SubscriberService subscribers, IEmailSender emailSender)
{
    // Onay mailiyle aynı gerekçe (bkz. SubscriberService.ConfirmationEmailCooldown) —
    // aynı e-postaya kısa sürede art arda kurtarma maili gitmesin diye.
    private static readonly TimeSpan RecoveryEmailCooldown = TimeSpan.FromMinutes(5);

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

    // Favorilerini kaydettiği cihaz/tarayıcıdaki token'ı kaybeden kullanıcı için
    // (localStorage temizlenmesi, farklı bir tarayıcı vb. — gerçek bir kullanıcı
    // raporuyla fark edildi) e-postasına token'ı içeren bir link gönderiyoruz.
    // Email enumeration'ı önlemek üzere (2026-08-15'teki token ifşası düzeltmesiyle
    // aynı gerekçe) bu metot subscriber bulunamazsa SESSİZCE hiçbir şey yapmıyor —
    // çağıran taraf (Program.cs) e-postanın kayıtlı olup olmadığından bağımsız
    // hep aynı genel mesajı dönüyor.
    public async Task SendRecoveryEmailAsync(string email, string frontendBaseUrl, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Email == normalized, cancellationToken);
        if (subscriber is null)
            return;

        if (subscriber.LastRecoveryEmailSentAt is { } lastSent && DateTimeOffset.UtcNow - lastSent < RecoveryEmailCooldown)
            return;

        var recoverUrl = $"{frontendBaseUrl}/favorilerim?recover={subscriber.Token}";
        // Onay mailiyle aynı email-safe düzen (inline-block/vertical-align,
        // flexbox yerine — bkz. SubscriberService.SendConfirmationEmailAsync).
        var html = $"""
            <div style="max-width:480px;margin:0 auto;padding:32px 24px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <div style="text-align:center;margin-bottom:24px;">
                <span style="display:inline-block;width:36px;height:36px;line-height:36px;border-radius:8px;background:#059669;color:#ffffff;font-weight:800;font-size:14px;text-align:center;vertical-align:middle;">PA</span>
                <span style="font-size:18px;font-weight:700;color:#1c1917;vertical-align:middle;margin-left:8px;">Protein<span style="color:#059669;">Avcısı</span></span>
              </div>
              <div style="background:#ffffff;border:1px solid #e7e5e4;border-radius:16px;padding:32px 28px;">
                <h1 style="font-size:18px;font-weight:800;color:#1c1917;margin:0 0 12px;">Favori listeni geri getir</h1>
                <p style="font-size:14px;color:#57534e;line-height:1.6;margin:0 0 20px;">
                  Bu cihazda favori ürünlerini göremiyor musun? Aşağıdaki butona tıklayınca bu tarayıcıda listen otomatik geri gelecek.
                </p>
                <div style="text-align:center;margin:24px 0;">
                  <a href="{recoverUrl}" style="display:inline-block;background:#059669;color:#ffffff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 32px;border-radius:9999px;">Favorilerimi Göster</a>
                </div>
                <p style="font-size:12px;color:#a8a29e;line-height:1.5;margin:20px 0 0;">
                  Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin, herhangi bir işlem yapılmayacak.
                </p>
              </div>
            </div>
            """;

        await emailSender.SendAsync(subscriber.Email, "Favori listeni geri getir", html, cancellationToken);

        subscriber.LastRecoveryEmailSentAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
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

using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

public record SubscribeRequest(string Email);

// Double opt-in zorunlu (İYS/KVKK gereği) — abone olma isteği direkt
// aktifleştirmiyor, onay linkine tıklanana kadar IsConfirmed=false
// kalıyor. Zaten onaylanmış bir e-posta tekrar abone olmaya çalışırsa
// sessizce görmezden geliniyor (spam gibi tekrar mail atmasın diye).
public class SubscriberService(AppDbContext db, IEmailSender emailSender)
{
    public async Task SubscribeAsync(SubscribeRequest request, string confirmBaseUrl, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Email == email, cancellationToken);
        if (subscriber is null)
        {
            subscriber = new Subscriber
            {
                Email = email,
                Token = Guid.NewGuid().ToString("N"),
                SubscribedAt = DateTimeOffset.UtcNow,
            };
            db.Subscribers.Add(subscriber);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (subscriber.IsConfirmed)
        {
            return;
        }

        var confirmUrl = $"{confirmBaseUrl}/api/subscribe/confirm/{subscriber.Token}";
        // E-posta istemcileri (Gmail dahil) flexbox'ı güvenilir desteklemiyor,
        // bu yüzden confirm sayfasındaki gibi değil, inline-block/vertical-align
        // ile daha "email-safe" bir düzen kullanıyoruz.
        var html = $"""
            <div style="max-width:480px;margin:0 auto;padding:32px 24px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <div style="text-align:center;margin-bottom:24px;">
                <span style="display:inline-block;width:36px;height:36px;line-height:36px;border-radius:8px;background:#059669;color:#ffffff;font-weight:800;font-size:14px;text-align:center;vertical-align:middle;">PA</span>
                <span style="font-size:18px;font-weight:700;color:#1c1917;vertical-align:middle;margin-left:8px;">Protein<span style="color:#059669;">Avcısı</span></span>
              </div>
              <div style="background:#ffffff;border:1px solid #e7e5e4;border-radius:16px;padding:32px 28px;">
                <h1 style="font-size:18px;font-weight:800;color:#1c1917;margin:0 0 12px;">Bültenimize hoş geldin!</h1>
                <p style="font-size:14px;color:#57534e;line-height:1.6;margin:0 0 20px;">
                  Protein Avcısı bültenine abone olmak için aşağıdaki butona tıkla. Onayladıktan sonra öne çıkan indirimlerden haberdar olacaksın.
                </p>
                <div style="text-align:center;margin:24px 0;">
                  <a href="{confirmUrl}" style="display:inline-block;background:#059669;color:#ffffff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 32px;border-radius:9999px;">Aboneliğimi Onayla</a>
                </div>
                <p style="font-size:12px;color:#a8a29e;line-height:1.5;margin:20px 0 0;">
                  Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin, herhangi bir işlem yapılmayacak.
                </p>
              </div>
            </div>
            """;

        await emailSender.SendAsync(subscriber.Email, "Protein Avcısı bültenine abone ol", html, cancellationToken);
    }

    public async Task<bool> ConfirmAsync(string token, CancellationToken cancellationToken = default)
    {
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
        if (subscriber is null)
            return false;

        subscriber.IsConfirmed = true;
        subscriber.ConfirmedAt = DateTimeOffset.UtcNow;
        subscriber.UnsubscribedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken = default)
    {
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
        if (subscriber is null)
            return false;

        // IsConfirmed'ı da geri alıyoruz — aksi halde tekrar abone olmaya
        // çalıştığında SubscribeAsync'teki "zaten onaylı" kısayolu devreye
        // girip yeni bir onay maili hiç gitmezdi.
        subscriber.UnsubscribedAt = DateTimeOffset.UtcNow;
        subscriber.IsConfirmed = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

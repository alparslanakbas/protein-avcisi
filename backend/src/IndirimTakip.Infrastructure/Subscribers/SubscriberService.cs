using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Subscribers;

public record SubscribeRequest(string Email);

// Double opt-in zorunlu (İYS/KVKK gereği) — abone olma isteği direkt
// aktifleştirmiyor, onay linkine tıklanana kadar IsConfirmed=false
// kalıyor. Zaten onaylanmış bir e-posta tekrar abone olmaya çalışırsa
// sessizce görmezden geliniyor (spam gibi tekrar mail atmasın diye).
public class SubscriberService(AppDbContext db, IEmailSender emailSender, ILogger<SubscriberService> logger)
{
    // 2026-08-15: /api/subscribe ve /api/products/{id}/watch, henuz onaylanmamis
    // bir abone icin CAGRILDIGI HER SEFERINDE onay maili gonderiyordu - rate
    // limit'siz bir uc, ayni e-postayla art arda cagrilinca (bot/kotu niyetli
    // istek) tek dakikada onlarca mail gitmesine, Brevo'nun o adresi kara
    // listeye almasina yol acti. Bu cooldown, IP-bazli rate limit'e (Program.cs)
    // ek bir savunma katmani - IP degistirilerek limit atlatilsa bile ayni
    // e-postaya kisa surede ikinci bir mail gitmiyor.
    private static readonly TimeSpan ConfirmationEmailCooldown = TimeSpan.FromMinutes(5);


    // Dönüş değeri: onay maili gerçekten gönderilebildi mi (zaten onaylıysa
    // gönderilecek bir şey olmadığı için true sayılır). Çağıran taraf
    // (Program.cs) false durumunda kullanıcıya "e-postanı kontrol et" gibi
    // yanıltıcı bir mesaj DEĞİL, dürüst bir "şu anda gönderilemiyor" hatası
    // dönmeli — aksi halde mail hiç gitmediği halde kullanıcı sonsuza dek
    // gelmeyecek bir onay linkini bekler.
    public async Task<bool> SubscribeAsync(SubscribeRequest request, string confirmBaseUrl, CancellationToken cancellationToken = default)
    {
        var subscriber = await GetOrCreateSubscriberAsync(request.Email, cancellationToken);
        if (subscriber.IsConfirmed)
            return true;

        return await SendConfirmationEmailAsync(subscriber, confirmBaseUrl, cancellationToken);
    }

    // "Haber Ver" (ürün fiyat izleme) gibi başka akışların da abone
    // kaydı oluşturması/bulması gerekiyor — aynı Subscriber tablosu ve
    // aynı onay süreci kullanılıyor, ayrı bir izin mekanizması gerekmedi.
    public async Task<Subscriber> GetOrCreateSubscriberAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Email == normalized, cancellationToken);
        if (subscriber is null)
        {
            subscriber = new Subscriber
            {
                Email = normalized,
                Token = Guid.NewGuid().ToString("N"),
                SubscribedAt = DateTimeOffset.UtcNow,
            };
            db.Subscribers.Add(subscriber);
            await db.SaveChangesAsync(cancellationToken);
        }

        return subscriber;
    }

    public async Task<bool> SendConfirmationEmailAsync(Subscriber subscriber, string confirmBaseUrl, CancellationToken cancellationToken = default)
    {
        if (subscriber.LastConfirmationEmailSentAt is { } lastSent && DateTimeOffset.UtcNow - lastSent < ConfirmationEmailCooldown)
            return true;

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

        // Brevo (üçüncü taraf) geçici olarak kullanılamaz hale gelebilir (API
        // anahtarı yanlış/süresi geçmiş, network sorunu, Brevo'nun kendi
        // downtime'ı). Bu durumda kullanıcıya çıplak bir 500 göstermek yerine
        // hatayı burada yakalayıp çağıran tarafın dürüst bir mesaj vermesine
        // izin veriyoruz — LastConfirmationEmailSentAt de SADECE gerçekten
        // gönderilebildiyse güncelleniyor (aksi halde kullanıcı 5 dakika
        // boşuna bekler, mail hiç gitmediği hâlde tekrar deneyemez).
        try
        {
            await emailSender.SendAsync(subscriber.Email, "Protein Avcısı bültenine abone ol", html, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Onay e-postası gönderilemedi: {Email}", subscriber.Email);
            return false;
        }

        subscriber.LastConfirmationEmailSentAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
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

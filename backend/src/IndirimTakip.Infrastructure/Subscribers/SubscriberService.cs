using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Subscribers;

public record SubscribeRequest(string Email);

// Double opt-in zorunlu (İYS/KVKK gereği) — abone olma isteği direkt
// aktifleştirmiyor, onay linkine tıklanana kadar IsConfirmed=false
// kalıyor. Zaten onaylanmış bir e-posta tekrar abone olmaya çalışırsa
// sessizce görmezden geliniyor (spam gibi tekrar mail atmasın diye).
public class SubscriberService(
    AppDbContext db,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<SubscriberService> logger)
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
        var frontendBaseUrl = configuration["FrontendBaseUrl"] ?? EmailTemplate.ProductionFrontendUrl;
        var html = BuildConfirmationHtml(confirmUrl, frontendBaseUrl);

        // Brevo (üçüncü taraf) geçici olarak kullanılamaz hale gelebilir (API
        // anahtarı yanlış/süresi geçmiş, network sorunu, Brevo'nun kendi
        // downtime'ı). Bu durumda kullanıcıya çıplak bir 500 göstermek yerine
        // hatayı burada yakalayıp çağıran tarafın dürüst bir mesaj vermesine
        // izin veriyoruz — LastConfirmationEmailSentAt de SADECE gerçekten
        // gönderilebildiyse güncelleniyor (aksi halde kullanıcı 5 dakika
        // boşuna bekler, mail hiç gitmediği hâlde tekrar deneyemez).
        try
        {
            await emailSender.SendAsync(subscriber.Email, "Protein Avcısı — Aboneliğini onayla", html, cancellationToken);
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

    private static string BuildConfirmationHtml(string confirmUrl, string frontendBaseUrl)
    {
        var signalImageUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "confirmation-price-signal.jpg");
        var confirmIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "step-confirm.png");
        var alertIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "step-alert.png");
        var shieldIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "trust-shield.png");

        var content = $"""
            <table class="email-shell" role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" bgcolor="#ffffff" style="width:600px;max-width:600px;background:#ffffff;border:1px solid #e4e6ef;border-radius:16px;overflow:hidden;box-shadow:0 16px 44px rgba(20,24,48,.10);">
              {EmailTemplate.BrandHeader(frontendBaseUrl)}
              <tr>
                <td class="email-hero" background="{EmailTemplate.Encode(signalImageUrl)}" bgcolor="#0e1122" style="padding:52px 42px 48px;background-color:#0e1122;background-image:url('{EmailTemplate.Encode(signalImageUrl)}');background-position:center;background-size:cover;background-repeat:no-repeat;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td width="70%" style="width:70%;font-family:Arial,Helvetica,sans-serif;">
                        <div style="display:inline-block;border:1px solid #796cbf;background:#2b2741;color:#f5f6fb;font-size:11px;font-weight:800;line-height:16px;letter-spacing:.8px;padding:7px 12px;border-radius:999px;">E-POSTA DOĞRULAMA</div>
                        <h1 class="email-title" style="margin:24px 0 14px;color:#ffffff;font-size:35px;font-weight:800;line-height:1.08;letter-spacing:-1.1px;">Gerçek indirimler<br>için son adım</h1>
                        <p style="margin:0 0 24px;color:#c7cbe0;font-size:16px;line-height:1.55;">E-posta adresini doğrula; haftanın öne çıkan fiyat düşüşlerini kaçırma.</p>
                        {EmailTemplate.PrimaryButton(confirmUrl, "Aboneliğimi Onayla")}
                      </td>
                      <td width="30%" class="mobile-hide" style="width:30%;">&nbsp;</td>
                    </tr>
                  </table>
                </td>
              </tr>
              <tr>
                <td class="email-pad" style="padding:30px 38px 28px;background:#ffffff;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td class="step-cell" width="50%" valign="top" style="width:50%;padding:4px 18px 18px 0;">
                        <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                          <tr>
                            <td valign="top" width="58"><img src="{EmailTemplate.Encode(confirmIconUrl)}" width="54" height="54" alt="" style="display:block;width:54px;height:54px;"></td>
                            <td valign="top" style="padding-left:10px;font-family:Arial,Helvetica,sans-serif;">
                              <div style="color:#171a2e;font-size:15px;font-weight:800;line-height:21px;">1. Onayla</div>
                              <div style="margin-top:5px;color:#60667a;font-size:13px;line-height:19px;">E-posta adresini onaylayarak aboneliğini güvence altına al.</div>
                            </td>
                          </tr>
                        </table>
                      </td>
                      <td class="step-cell" width="50%" valign="top" style="width:50%;padding:4px 0 18px 18px;">
                        <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                          <tr>
                            <td valign="top" width="58"><img src="{EmailTemplate.Encode(alertIconUrl)}" width="54" height="54" alt="" style="display:block;width:54px;height:54px;"></td>
                            <td valign="top" style="padding-left:10px;font-family:Arial,Helvetica,sans-serif;">
                              <div style="color:#171a2e;font-size:15px;font-weight:800;line-height:21px;">2. İndirimleri al</div>
                              <div style="margin-top:5px;color:#60667a;font-size:13px;line-height:19px;">Haftanın öne çıkan fiyat düşüşleri e-postana gelsin.</div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                  <div style="height:1px;background:#e4e6ef;margin:2px 0 20px;line-height:1px;">&nbsp;</div>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td width="44" valign="middle"><img src="{EmailTemplate.Encode(shieldIconUrl)}" width="38" height="38" alt="" style="display:block;width:38px;height:38px;"></td>
                      <td valign="middle" style="padding-left:10px;font-family:Arial,Helvetica,sans-serif;color:#303548;font-size:12px;line-height:18px;">
                        Bu isteği sen yapmadıysan e-postayı yok sayabilirsin.<br>
                        <a href="{EmailTemplate.Encode(frontendBaseUrl)}" style="color:#6556e8;font-weight:700;text-decoration:none;">proteinavcisi.com.tr</a>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;

        return EmailTemplate.Document(
            "Gerçek fiyat düşüşlerini almak için aboneliğini tek tıkla onayla.",
            content);
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

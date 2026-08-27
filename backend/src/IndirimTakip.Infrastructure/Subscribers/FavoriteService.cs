using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

public record FavoriteRequest(string? Token, string? Email);

// ResolveSubscriberAsync'in aboneyi HANGİ yoldan bulduğunu ayırt etmek için —
// AddAsync sadece "yeni mi değil mi" bilmek yetmiyor: e-posta zaten var olan
// bir aboneye ait çıktığında (ByExistingEmail), bu cihazda token hiç yoktur,
// favorinin eklendiği "görünmez" kalır (bkz. 2026-08-18'de gerçek bir
// kullanıcı raporuyla bulunan bug: favori sunucuda ekleniyordu ama bu
// cihaz hiç token almadığı için /favorilerim boş görünüyordu).
internal enum SubscriberResolution { ByToken, ByExistingEmail, NewlyCreated }

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
    // gerçekten YENİ oluşturulan bir abone için dönüyor.
    // 2026-08-18: e-posta zaten var olan bir aboneye aitse (ByExistingEmail)
    // favori yine ekleniyor ama bu cihazda hiç token yok — kullanıcı
    // gerçek bir testte bunu "favori eklendi ama listede hiç görünmüyor"
    // olarak yaşadı. Artık bu durumda otomatik olarak aynı kurtarma
    // maili gönderiliyor (RecoverySent=true) ki kullanıcı bu cihazı da
    // aynı e-postayla kurtarabilsin.
    public async Task<(bool Success, string? Token, bool RecoverySent)> AddAsync(
        int productId, string? token, string? email, string frontendBaseUrl, CancellationToken cancellationToken = default)
    {
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
            return (false, null, false);

        var (subscriber, resolution) = await ResolveSubscriberAsync(token, email, cancellationToken);
        if (subscriber is null)
            return (false, null, false);

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

        var recoverySent = false;
        if (resolution == SubscriberResolution.ByExistingEmail)
        {
            try
            {
                // Cooldown SendRecoveryEmailAsync içinde zaten kontrol ediliyor
                // (kısa süre önce gerçek bir kurtarma maili gittiyse burada
                // sessizce hiçbir şey göndermez) — favori her durumda eklenmiş
                // sayılır, e-posta gönderiminin başarısız olması bunu bozmasın
                // diye hatayı yutuyoruz.
                await SendRecoveryEmailAsync(subscriber.Email, frontendBaseUrl, cancellationToken);
                recoverySent = true;
            }
            catch
            {
                // yutuluyor — bkz. yukarıdaki açıklama.
            }
        }

        return (true, resolution == SubscriberResolution.NewlyCreated ? subscriber.Token : null, recoverySent);
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

        var recoverUrl = $"{frontendBaseUrl.TrimEnd('/')}/favorilerim?recover={subscriber.Token}";
        var shieldIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "trust-shield.png");
        var confirmIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "step-confirm.png");
        var content = $"""
            <table class="email-shell" role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" bgcolor="#ffffff" style="width:600px;max-width:600px;background:#ffffff;border:1px solid #e4e6ef;border-radius:16px;overflow:hidden;box-shadow:0 16px 44px rgba(20,24,48,.10);">
              {EmailTemplate.BrandHeader(frontendBaseUrl)}
              <tr>
                <td class="email-hero" bgcolor="#0e1122" style="padding:42px 38px;font-family:Arial,Helvetica,sans-serif;">
                  <div style="display:inline-block;border:1px solid #796cbf;background:#2b2741;color:#f5f6fb;font-size:11px;font-weight:800;line-height:16px;letter-spacing:.8px;padding:7px 12px;border-radius:999px;">FAVORİ LİSTESİ</div>
                  <h1 class="email-title" style="margin:20px 0 12px;color:#ffffff;font-size:34px;font-weight:800;line-height:1.1;letter-spacing:-1px;">Favori listeni geri getir</h1>
                  <p style="max-width:440px;margin:0;color:#c7cbe0;font-size:15px;line-height:23px;">Bu cihazda favori ürünlerini göremiyorsan listen tek tıkla yeniden bağlanacak.</p>
                </td>
              </tr>
              <tr>
                <td class="email-pad" bgcolor="#ffffff" style="padding:30px 38px 28px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td width="64" valign="top"><img src="{EmailTemplate.Encode(confirmIconUrl)}" width="56" height="56" alt="" style="display:block;width:56px;height:56px;"></td>
                      <td valign="top" style="padding-left:12px;font-family:Arial,Helvetica,sans-serif;">
                        <div style="color:#171a2e;font-size:16px;font-weight:800;line-height:22px;">Bu tarayıcıyı listenle eşleştir</div>
                        <div style="margin-top:6px;color:#60667a;font-size:13px;line-height:20px;">Bağlantıya tıklayınca favorilerin bu cihazda otomatik olarak görünecek.</div>
                      </td>
                    </tr>
                  </table>
                  <div style="margin-top:24px;text-align:center;">{EmailTemplate.PrimaryButton(recoverUrl, "Favorilerimi Göster")}</div>
                  <p style="margin:24px 0 0;padding-top:20px;border-top:1px solid #e4e6ef;color:#60667a;font-family:Arial,Helvetica,sans-serif;font-size:11px;line-height:17px;">Birden fazla tarayıcı kullanıyorsan bu bağlantıya her birinden ayrı ayrı tıklaman gerekir; her tarayıcı kendi listesini ayrı hatırlar.</p>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:16px;">
                    <tr>
                      <td width="38" valign="middle"><img src="{EmailTemplate.Encode(shieldIconUrl)}" width="30" height="30" alt="" style="display:block;width:30px;height:30px;"></td>
                      <td valign="middle" style="padding-left:8px;color:#60667a;font-family:Arial,Helvetica,sans-serif;font-size:11px;line-height:16px;">Bu isteği sen yapmadıysan e-postayı yok sayabilirsin.</td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;
        var html = EmailTemplate.Document(
            "Favori ürünlerini bu tarayıcıda yeniden görmek için listeni geri getir.",
            content);

        await emailSender.SendAsync(subscriber.Email, "Favori listeni geri getir", html, cancellationToken);

        subscriber.LastRecoveryEmailSentAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Subscriber? Subscriber, SubscriberResolution Resolution)> ResolveSubscriberAsync(string? token, string? email, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(token))
        {
            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
            if (existing is not null)
                return (existing, SubscriberResolution.ByToken);
        }

        if (string.IsNullOrWhiteSpace(email))
            return (null, SubscriberResolution.ByToken);

        var normalized = email.Trim().ToLowerInvariant();
        var alreadyExisted = await db.Subscribers.AnyAsync(s => s.Email == normalized, cancellationToken);
        var subscriber = await subscribers.GetOrCreateSubscriberAsync(email, cancellationToken);
        return (subscriber, alreadyExisted ? SubscriberResolution.ByExistingEmail : SubscriberResolution.NewlyCreated);
    }
}

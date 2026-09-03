using System.Net.Mail;

namespace IndirimTakip.Api.Endpoints;

// Uç tanımlarının paylaştığı yardımcılar. Program.cs'te üst seviye yerel
// fonksiyonlardı; uçlar ayrı dosyalara çıkınca oradan erişilemez oldukları
// için ortak bir sınıfa alındılar. Davranışları değişmedi.
internal static class EndpointHelpers
{
    // Başarılı onay durumu, kullanıcı e-postada gördüğü Hybrid Nocturne görsel
    // dilinden kopmadan doğrudan güncel indirimlere dönebilsin diye ayrı ve daha
    // güçlü bir başarı yüzeyi kullanıyor. Geçersiz link ve bültenden çıkış gibi
    // nötr durumlar aşağıdaki kompakt bilgi sayfasını kullanmaya devam ediyor.
    internal static string BuildSubscriptionConfirmedPage(string frontendBaseUrl)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        var signalImageUrl = $"{baseUrl}/email-assets/subscription-confirmed-signal.jpg";
        var logoUrl = $"{baseUrl}/favicon.svg";
        var mailIconUrl = $"{baseUrl}/email-assets/step-confirm.png";
        var shieldImageUrl = $"{baseUrl}/email-assets/trust-shield.png";

        return $$"""
            <!doctype html>
            <html lang="tr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="light only">
              <title>Aboneliğin onaylandı! — Protein Avcısı</title>
              <style>
                * { box-sizing:border-box; }
                html, body { min-height:100%; }
                body {
                  margin:0;
                  background:#f7f7fc;
                  color:#171a2e;
                  font-family:Arial,Helvetica,sans-serif;
                  -webkit-font-smoothing:antialiased;
                }
                .confirmation-page {
                  min-height:100vh;
                  display:flex;
                  flex-direction:column;
                  align-items:center;
                  justify-content:flex-start;
                  padding:48px 24px 56px;
                }
                .brand-link {
                  display:inline-flex;
                  align-items:center;
                  gap:14px;
                  margin-bottom:36px;
                  color:#171a2e;
                  text-decoration:none;
                }
                .brand-link img { width:58px; height:58px; display:block; }
                .brand-name {
                  font-size:32px;
                  font-weight:800;
                  line-height:1;
                  letter-spacing:-1.2px;
                  white-space:nowrap;
                }
                .brand-name span { color:#6556e8; }
                .confirmation-card {
                  width:min(100%, 1120px);
                  overflow:hidden;
                  background:#ffffff;
                  border:1px solid #e4e6ef;
                  border-radius:20px;
                  box-shadow:0 18px 54px rgba(20,24,48,.14);
                }
                .confirmation-hero {
                  min-height:675px;
                  padding:80px 86px;
                  background-color:#0e1122;
                  background-image:url('{{signalImageUrl}}');
                  background-position:center;
                  background-repeat:no-repeat;
                  background-size:cover;
                }
                .confirmation-content { width:44%; }
                .eyebrow {
                  display:inline-flex;
                  align-items:center;
                  gap:10px;
                  padding:12px 20px 12px 14px;
                  border:1px solid #796cbf;
                  border-radius:999px;
                  background:#2b2741;
                  color:#f5f6fb;
                  font-size:17px;
                  font-weight:800;
                  line-height:22px;
                  letter-spacing:.9px;
                }
                .eyebrow img { width:28px; height:28px; display:block; }
                h1 {
                  margin:32px 0 22px;
                  color:#ffffff;
                  font-size:72px;
                  font-weight:800;
                  line-height:1.04;
                  letter-spacing:-2.1px;
                }
                .confirmation-copy {
                  margin:0;
                  color:#c7cbe0;
                  font-size:25px;
                  line-height:1.5;
                }
                .primary-action {
                  display:block;
                  width:100%;
                  margin-top:36px;
                  padding:26px;
                  border-radius:10px;
                  background:#6556e8;
                  color:#ffffff;
                  font-size:22px;
                  font-weight:800;
                  line-height:24px;
                  text-align:center;
                  text-decoration:none;
                  box-shadow:0 10px 24px rgba(101,86,232,.24);
                  transition:background-color .18s ease, transform .18s ease;
                }
                .primary-action:hover { background:#7567ec; transform:translateY(-1px); }
                .primary-action:focus-visible { outline:3px solid #b5abfc; outline-offset:4px; }
                .trust-strip {
                  display:flex;
                  align-items:center;
                  gap:18px;
                  min-height:145px;
                  padding:38px 86px;
                  background:#ffffff;
                  color:#303548;
                  font-size:19px;
                  line-height:1.5;
                }
                .trust-strip img { width:76px; height:76px; display:block; flex:0 0 auto; }
                @media (max-width:760px) {
                  .confirmation-page { justify-content:flex-start; padding:28px 14px; }
                  .brand-link { margin-bottom:24px; gap:10px; }
                  .brand-link img { width:44px; height:44px; }
                  .brand-name { font-size:23px; letter-spacing:-.8px; }
                  .confirmation-card { border-radius:16px; }
                  .confirmation-hero {
                    min-height:630px;
                    padding:42px 26px 270px;
                    background-position:67% center;
                  }
                  .confirmation-content { width:100%; }
                  .eyebrow { padding:8px 13px 8px 9px; gap:8px; font-size:12px; line-height:18px; }
                  .eyebrow img { width:22px; height:22px; }
                  h1 { margin-top:22px; font-size:40px; line-height:1.06; letter-spacing:-1.3px; }
                  .confirmation-copy { font-size:17px; line-height:1.5; }
                  .primary-action { margin-top:26px; padding:18px 26px; font-size:17px; }
                  .trust-strip { align-items:flex-start; padding:24px 24px; gap:12px; font-size:14px; }
                  .trust-strip img { width:42px; height:42px; }
                }
                @media (max-width:380px) {
                  .brand-name { font-size:21px; }
                  .confirmation-hero { padding-left:22px; padding-right:22px; }
                  h1 { font-size:36px; }
                }
              </style>
            </head>
            <body>
              <main class="confirmation-page">
                <a class="brand-link" href="{{baseUrl}}" aria-label="Protein Avcısı ana sayfası">
                  <img src="{{logoUrl}}" width="54" height="54" alt="">
                  <span class="brand-name">PROTEİN<span>AVCISI</span></span>
                </a>
                <section class="confirmation-card" aria-labelledby="confirmation-heading">
                  <div class="confirmation-hero">
                    <div class="confirmation-content">
                      <div class="eyebrow"><img src="{{mailIconUrl}}" width="24" height="24" alt="">ABONELİK AKTİF</div>
                      <h1 id="confirmation-heading">Aboneliğin<br>onaylandı!</h1>
                      <p class="confirmation-copy">Artık gerçek fiyat düşüşleri ve haftanın öne çıkan fırsatları e&#8209;postana gelecek.</p>
                      <a class="primary-action" href="{{baseUrl}}">İndirimleri Gör</a>
                    </div>
                  </div>
                  <div class="trust-strip">
                    <img src="{{shieldImageUrl}}" width="52" height="52" alt="">
                    <span>E-posta tercihini dilediğin zaman değiştirebilirsin.</span>
                  </div>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    // frontendBaseUrl config'ten geliyor — bu proje domainini bu oturumda 2 kez
    // değiştirdi, hardcoded bir adresin unutulup eskide kalması gerçek bir risk.
    internal static string BuildInfoPage(string heading, string message, string frontendBaseUrl) => $"""
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Protein Avcısı</title>
        </head>
        <body style="margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px;box-sizing:border-box;background:#fafaf9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <div style="max-width:420px;width:100%;background:#ffffff;border-radius:16px;box-shadow:0 4px 24px rgba(0,0,0,0.08);padding:40px 32px;text-align:center;">
            <div style="display:inline-flex;align-items:center;gap:8px;margin-bottom:28px;">
              <div style="width:36px;height:36px;border-radius:8px;background:#059669;color:#fff;font-weight:800;font-size:14px;display:flex;align-items:center;justify-content:center;">PA</div>
              <span style="font-size:18px;font-weight:700;color:#1c1917;">Protein<span style="color:#059669;">Avcısı</span></span>
            </div>
            <h1 style="font-size:20px;font-weight:800;color:#1c1917;margin:0 0 8px;">{heading}</h1>
            <p style="font-size:14px;color:#78716c;margin:0 0 28px;line-height:1.5;">{message}</p>
            <a href="{frontendBaseUrl}" style="display:inline-block;background:#059669;color:#fff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 28px;border-radius:9999px;">Siteye Dön</a>
          </div>
        </body>
        </html>
        """;

    // 2026-08-15: sadece "@" içeriyor mu kontrolü, "a;b@x.com" / "a\"b@x.com" gibi
    // RFC 5322'ye göre bile geçersiz string'lerin Subscribers tablosuna girmesine
    // izin veriyordu (bir güvenlik açığı arayan biri bunları test etmişti — bkz.
    // CLAUDE.md). SQL injection zaten EF Core'un parametreli sorguları sayesinde
    // mümkün değildi, bu sadece veri hijyeni için — MailAddress'in kendi format
    // doğrulamasına güveniyoruz, ayrı bir regex bakımı gerekmiyor.
    internal static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            return new System.Net.Mail.MailAddress(email).Address == email.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // 2026-08-15 güvenlik denetimi: pageSize'a hiç üst sınır yoktu (ör.
    // ?pageSize=5000000 gibi bir istek büyük bir sıralı sorguya yol açabilirdi).

    // 2026-08-15 güvenlik denetimi: pageSize'a hiç üst sınır yoktu (ör.
    // ?pageSize=5000000 gibi bir istek büyük bir sıralı sorguya yol açabilirdi).
    internal static int NormalizePageSize(int? pageSize) => pageSize is null or <= 0 ? 24 : Math.Min(pageSize.Value, 100);
}

using System.Globalization;
using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IndirimTakip.Infrastructure.Subscribers;

// Tarama döngüsünün bir parçası olarak çağrılıyor (bkz. ScrapeIngestionService).
// Sadece o taramada FİYATI DEĞİŞEN ürünler arasında aktif ("Haber Ver" tıklanmış
// ama henüz bildirilmemiş) izleme olan ürünleri kontrol ediyor — 600+ ürünün
// tamamı için değil, sadece gerçekten izlenen küçük bir alt küme için sorgu
// çalıştırıyor.
public class ProductWatchNotifier(AppDbContext db, IEmailSender emailSender, IConfiguration configuration)
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task CheckAndNotifyAsync(IReadOnlyCollection<int> touchedProductIds, CancellationToken cancellationToken = default)
    {
        if (touchedProductIds.Count == 0)
            return;

        var activeWatches = await db.ProductWatches
            .Where(w => w.NotifiedAt == null && touchedProductIds.Contains(w.ProductId) && w.Subscriber!.IsConfirmed)
            .Include(w => w.Subscriber)
            .Include(w => w.Product)
            .ToListAsync(cancellationToken);

        if (activeWatches.Count == 0)
            return;

        var frontendBaseUrl = configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr";

        foreach (var group in activeWatches.GroupBy(w => w.ProductId))
        {
            var lastTwoPrices = await db.PriceHistories
                .Where(ph => ph.ProductId == group.Key)
                .OrderByDescending(ph => ph.ScrapedAt)
                .Take(2)
                .Select(ph => ph.Price)
                .ToListAsync(cancellationToken);

            // İki fiyat noktası yoksa (ürün yeni eklendiyse) ya da fiyat
            // düşmediyse bildirim gönderilmiyor — izleme aktif kalıyor,
            // gerçekten düşünce tetiklenecek.
            if (lastTwoPrices.Count < 2 || lastTwoPrices[0] >= lastTwoPrices[1])
                continue;

            var newPrice = lastTwoPrices[0];
            var oldPrice = lastTwoPrices[1];

            foreach (var watch in group)
            {
                var html = BuildNotifyHtml(watch.Product!, oldPrice, newPrice, frontendBaseUrl);
                try
                {
                    await emailSender.SendAsync(watch.Subscriber!.Email, $"{watch.Product!.Name} fiyatı düştü!", html, cancellationToken);
                    watch.NotifiedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception)
                {
                    // Bu abonenin gönderimi başarısız oldu — izleme aktif kalsın,
                    // bir sonraki tarama döngüsünde tekrar denenecek. Diğer
                    // abonelerin/ürünlerin bildirimini engellemesin diye devam.
                }
            }

            // Grup bazında kaydediyoruz — döngü ortasında bir grup patlarsa
            // önceki gruplarda başarıyla gönderilmiş bildirimlerin NotifiedAt
            // işaretlemesi kaybolmasın diye tek bir toplu SaveChanges yerine.
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildNotifyHtml(Product product, decimal oldPrice, decimal newPrice, string frontendBaseUrl)
    {
        var productUrl = $"{frontendBaseUrl.TrimEnd('/')}/urun/{product.Id}";
        var signalImageUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "confirmation-price-signal.jpg");
        var shieldIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "trust-shield.png");
        var imageHtml = product.ImageUrl is not null
            ? $"""<img src="{EmailTemplate.Encode(product.ImageUrl)}" alt="{EmailTemplate.Encode(product.Name)}" width="112" height="112" style="display:block;width:112px;height:112px;object-fit:contain;background:#ffffff;margin:0 auto;" />"""
            : """<div style="width:112px;height:112px;background:#f7f8fc;margin:0 auto;"></div>""";

        var content = $"""
            <table class="email-shell" role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" bgcolor="#ffffff" style="width:600px;max-width:600px;background:#ffffff;border:1px solid #e4e6ef;border-radius:16px;overflow:hidden;box-shadow:0 16px 44px rgba(20,24,48,.10);">
              {EmailTemplate.BrandHeader(frontendBaseUrl)}
              <tr>
                <td class="email-hero" background="{EmailTemplate.Encode(signalImageUrl)}" bgcolor="#0e1122" style="padding:40px 38px;background-color:#0e1122;background-image:url('{EmailTemplate.Encode(signalImageUrl)}');background-position:center;background-size:cover;background-repeat:no-repeat;font-family:Arial,Helvetica,sans-serif;">
                  <div style="display:inline-block;border:1px solid #796cbf;background:#2b2741;color:#f5f6fb;font-size:11px;font-weight:800;line-height:16px;letter-spacing:.8px;padding:7px 12px;border-radius:999px;">FİYAT ALARMI</div>
                  <h1 class="email-title" style="max-width:360px;margin:20px 0 10px;color:#ffffff;font-size:34px;font-weight:800;line-height:1.1;letter-spacing:-1px;">Takip ettiğin ürünün fiyatı düştü</h1>
                  <p style="max-width:330px;margin:0;color:#c7cbe0;font-size:14px;line-height:21px;">Yeni fiyatı ve ürün detaylarını aşağıda görebilirsin.</p>
                </td>
              </tr>
              <tr>
                <td class="email-pad" align="center" bgcolor="#ffffff" style="padding:30px 38px 28px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td class="mobile-block" width="126" valign="middle" align="center">{imageHtml}</td>
                      <td class="mobile-block mobile-center" valign="middle" style="padding-left:20px;font-family:Arial,Helvetica,sans-serif;">
                        <div style="color:#171a2e;font-size:16px;font-weight:800;line-height:22px;">{EmailTemplate.Encode(product.Name)}</div>
                        <div style="margin-top:12px;">
                          <span style="color:#70768a;text-decoration:line-through;font-size:13px;line-height:18px;">{oldPrice.ToString("N2", TurkishCulture)} TL</span>
                          <span style="display:inline-block;margin-left:8px;color:#168453;font-size:20px;font-weight:800;line-height:26px;">{newPrice.ToString("N2", TurkishCulture)} TL</span>
                        </div>
                        <div style="margin-top:18px;">{EmailTemplate.PrimaryButton(productUrl, "Ürünü İncele")}</div>
                      </td>
                    </tr>
                  </table>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:26px;border-top:1px solid #e4e6ef;">
                    <tr>
                      <td width="38" valign="middle" style="padding-top:18px;"><img src="{EmailTemplate.Encode(shieldIconUrl)}" width="30" height="30" alt="" style="display:block;width:30px;height:30px;"></td>
                      <td valign="middle" style="padding:18px 0 0 8px;font-family:Arial,Helvetica,sans-serif;color:#60667a;font-size:11px;line-height:16px;text-align:left;">Bu tek seferlik bir bildirimdir. Yeniden haber almak istersen ürün sayfasından tekrar “Haber Ver” seçeneğini kullanabilirsin.</td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;

        return EmailTemplate.Document(
            $"{product.Name} için takip ettiğin fiyat düştü.",
            content);
    }
}

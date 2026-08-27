using System.Globalization;
using System.Text;
using IndirimTakip.Infrastructure.Deals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IndirimTakip.Infrastructure.Subscribers;

public record DigestResult(int DealCount, int SubscriberCount, int PendingCount = 0);

// Kişiye özel ürün alarmı DEĞİL (bkz. CLAUDE.md) — zamanlanmış tarama
// döngüsünün tespit ettiği en yüksek indirimlerden genel bir özet, tüm
// onaylı abonelere aynı içerikle gönderiliyor. Sadece abonelikten çıkma
// linki her abone için kişiye özel (kendi token'ı).
public class DigestService(AppDbContext db, DealsQueryService dealsQuery, IEmailSender emailSender, IConfiguration configuration)
{
    private const int FeaturedDealCount = 6;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    // Brevo'nun ücretsiz katmanı günde 300 e-posta veriyor ve bu kota bültenle
    // transactional mailler (abonelik onayı, fiyat alarmı, favori kurtarma)
    // arasında PAYLAŞILIYOR. Bültenin tüm kotayı yiyip yeni bir abonenin onay
    // mailini engellememesi için burada bilinçli olarak bir tavan var; kalan
    // aboneler ertesi gün kaldığı yerden alıyor (LastDigestSentAt sayesinde).
    private const int DefaultDailyQuota = 200;

    public async Task<DigestResult> SendDigestAsync(string unsubscribeBaseUrl, CancellationToken cancellationToken = default)
    {
        var intervalDays = configuration.GetValue("Digest:IntervalDays", 7);
        var dailyQuota = configuration.GetValue("Digest:DailyQuota", DefaultDailyQuota);
        var now = DateTimeOffset.UtcNow;
        var dueBefore = now.AddDays(-intervalDays);

        // Bu turda kimlere gitmeli: onaylı, çıkmamış ve bu bülten periyodunda
        // henüz mail almamış aboneler.
        var pendingQuery = db.Subscribers
            .Where(s => s.IsConfirmed && s.UnsubscribedAt == null)
            .Where(s => s.LastDigestSentAt == null || s.LastDigestSentAt < dueBefore);

        var pendingCount = await pendingQuery.CountAsync(cancellationToken);
        if (pendingCount == 0)
            return new DigestResult(0, 0);

        // Bugün bültenden kaç mail çıktığını abonelerin kendi damgasından
        // sayıyoruz — ayrı bir sayaç tablosu tutmaya gerek yok ve restart'tan
        // etkilenmiyor.
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var sentToday = await db.Subscribers
            .CountAsync(s => s.LastDigestSentAt >= todayStart, cancellationToken);

        var remainingQuota = dailyQuota - sentToday;
        if (remainingQuota <= 0)
            return new DigestResult(0, 0, pendingCount);

        var deals = await dealsQuery.GetDealsAsync(
            referenceWindowDays: 30, brands: null, categories: null, search: null,
            minPrice: null, maxPrice: null, onlyDiscounted: true, onlyStoreDiscounted: false,
            sortBy: null, page: 1, pageSize: FeaturedDealCount, cancellationToken);

        // Gösterecek gerçek bir indirim yoksa boş/anlamsız bir e-posta göndermek
        // yerine hiç göndermiyoruz.
        if (deals.Items.Count == 0)
            return new DigestResult(0, 0, pendingCount);

        var subscribers = await pendingQuery
            .OrderBy(s => s.LastDigestSentAt == null ? 0 : 1).ThenBy(s => s.Id)
            .Take(remainingQuota)
            .ToListAsync(cancellationToken);

        var frontendBaseUrl = configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr";
        var dealsHtml = BuildDealGridHtml(deals.Items, frontendBaseUrl);

        // Her göndermeyi kendi try/catch'ine alıyoruz — bir alıcının gönderimi
        // (ör. Brevo'dan geçici bir hata) patlarsa listedeki diğer abonelerin
        // de o haftaki bülteni hiç almaması gibi ciddi bir sonuca yol açmasın.
        var sentCount = 0;
        foreach (var subscriber in subscribers)
        {
            var unsubscribeUrl = $"{unsubscribeBaseUrl}/api/subscribe/unsubscribe/{subscriber.Token}";
            var html = BuildDigestHtml(dealsHtml, unsubscribeUrl, frontendBaseUrl);
            try
            {
                await emailSender.SendAsync(subscriber.Email, "Protein Avcısı — Bu Haftanın Öne Çıkan İndirimleri", html, cancellationToken);
                // Damgayı yalnızca gönderim GERÇEKTEN başarılıysa atıyoruz;
                // hata alan abone bir sonraki turda tekrar sıraya giriyor.
                subscriber.LastDigestSentAt = now;
                sentCount++;
            }
            catch (Exception)
            {
                // Tek bir abonenin gönderimi başarısız olsa da döngü devam etsin.
            }
        }

        if (sentCount > 0)
            await db.SaveChangesAsync(cancellationToken);

        return new DigestResult(deals.Items.Count, sentCount, pendingCount - sentCount);
    }

    private static string BuildDealGridHtml(IReadOnlyList<DealDto> deals, string frontendBaseUrl)
    {
        var html = new StringBuilder();

        for (var index = 0; index < deals.Count; index += 2)
        {
            html.Append("<tr>");
            html.Append(BuildDealCardHtml(deals[index], frontendBaseUrl, isLeftColumn: true));

            if (index + 1 < deals.Count)
                html.Append(BuildDealCardHtml(deals[index + 1], frontendBaseUrl, isLeftColumn: false));
            else
                html.Append("<td class=\"product-cell\" width=\"50%\" style=\"width:50%;\"></td>");

            html.Append("</tr>");
        }

        return html.ToString();
    }

    private static string BuildDealCardHtml(DealDto deal, string frontendBaseUrl, bool isLeftColumn)
    {
        var productUrl = $"{frontendBaseUrl.TrimEnd('/')}/urun/{deal.ProductId}";
        var imageHtml = deal.ImageUrl is not null
            ? $"""<img class="product-image" src="{EmailTemplate.Encode(deal.ImageUrl)}" alt="" width="96" height="96" style="display:block;width:96px;height:96px;object-fit:contain;background:#ffffff;" />"""
            : """<div class="product-image" style="width:96px;height:96px;background:#f7f8fc;"></div>""";
        var referencePrice = deal.ReferencePrice.ToString("N2", TurkishCulture);
        var currentPrice = deal.CurrentPrice.ToString("N2", TurkishCulture);
        var discountPercent = Math.Round(deal.DiscountPercent);
        var rightBorder = isLeftColumn ? "border-right:1px solid #e4e6ef;" : string.Empty;

        return $"""
            <td class="product-cell" width="50%" valign="top" style="width:50%;padding:0;{rightBorder}border-bottom:1px solid #e4e6ef;">
              <table class="product-card" role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="min-height:190px;">
                <tr>
                  <td class="email-pad" valign="top" style="padding:22px 18px;">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                      <tr>
                        <td width="102" valign="top"><a href="{EmailTemplate.Encode(productUrl)}" style="text-decoration:none;">{imageHtml}</a></td>
                        <td valign="top" style="padding-left:12px;font-family:Arial,Helvetica,sans-serif;">
                          <a href="{EmailTemplate.Encode(productUrl)}" style="display:block;color:#171a2e;text-decoration:none;font-size:13px;font-weight:800;line-height:1.35;">{EmailTemplate.Encode(deal.ProductName)}</a>
                          <div style="margin-top:4px;color:#70768a;font-size:11px;font-weight:700;line-height:16px;text-transform:uppercase;">{EmailTemplate.Encode(deal.BrandName)}</div>
                          <div style="margin-top:14px;color:#70768a;text-decoration:line-through;font-size:11px;line-height:16px;">{referencePrice} TL</div>
                          <div style="margin-top:2px;color:#168453;font-size:17px;font-weight:800;line-height:22px;white-space:nowrap;">{currentPrice} TL</div>
                          <div style="display:inline-block;margin-top:6px;background:#dff7e8;color:#168453;font-size:11px;font-weight:800;line-height:16px;padding:3px 8px;border-radius:6px;">-%{discountPercent}</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </td>
            """;
    }

    // Deal kartları için <table> düzeni bilinçli — e-posta istemcileri arasında
    // (özellikle görsel + metnin yan yana durduğu bu tarz çok-sütunlu
    // yerleşimlerde) en güvenilir sonucu tablo veriyor, flex/grid değil.
    private static string BuildDigestHtml(string dealsHtml, string unsubscribeUrl, string frontendBaseUrl)
    {
        var tagImageUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "weekly-price-tag.png");
        var shieldIconUrl = EmailTemplate.AssetUrl(frontendBaseUrl, "trust-shield.png");

        var content = $"""
            <table class="email-shell" role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" bgcolor="#ffffff" style="width:600px;max-width:600px;background:#ffffff;border:1px solid #e4e6ef;border-radius:16px;overflow:hidden;box-shadow:0 16px 44px rgba(20,24,48,.10);">
              {EmailTemplate.BrandHeaderDark(frontendBaseUrl)}
              <tr>
                <td bgcolor="#0e1122" style="padding:20px 38px 34px;border-bottom:4px solid #6556e8;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td class="mobile-block mobile-center" width="68%" valign="middle" style="width:68%;font-family:Arial,Helvetica,sans-serif;">
                        <h1 class="email-title" style="margin:0;color:#ffffff;font-size:34px;font-weight:800;line-height:1.1;letter-spacing:-1px;">Haftalık fiyat özeti</h1>
                        <p style="margin:12px 0 0;color:#c7cbe0;font-size:14px;line-height:21px;">Son 30 günlük geçmişte öne çıkan 6 gerçek düşüş</p>
                      </td>
                      <td class="mobile-hide" width="32%" align="right" valign="middle" style="width:32%;padding-left:12px;">
                        <img src="{EmailTemplate.Encode(tagImageUrl)}" width="150" height="113" alt="" style="display:block;width:150px;height:113px;object-fit:cover;">
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
              <tr>
                <td bgcolor="#ffffff" style="padding:0;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    {dealsHtml}
                  </table>
                </td>
              </tr>
              <tr>
                <td class="email-pad" align="center" bgcolor="#ffffff" style="padding:28px 38px 20px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td align="center">{EmailTemplate.FullWidthButton(frontendBaseUrl, "Tüm İndirimleri Gör")}</td>
                    </tr>
                    <tr>
                      <td style="padding-top:22px;">
                        <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center">
                          <tr>
                            <td width="34" valign="middle"><img src="{EmailTemplate.Encode(shieldIconUrl)}" width="30" height="30" alt="" style="display:block;width:30px;height:30px;"></td>
                            <td valign="middle" style="padding-left:8px;font-family:Arial,Helvetica,sans-serif;color:#60667a;font-size:11px;line-height:16px;text-align:left;">Fiyatlar ilgili markaların kendi web sitelerinden<br class="mobile-hide"> otomatik olarak toplanmaktadır.</td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                    <tr>
                      <td align="center" style="padding-top:18px;border-top:1px solid #e5e0ff;font-family:Arial,Helvetica,sans-serif;font-size:11px;line-height:16px;">
                        <a href="{EmailTemplate.Encode(unsubscribeUrl)}" style="color:#6556e8;text-decoration:underline;">Bültenden çık</a>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;

        return EmailTemplate.Document(
            "Son 30 günlük fiyat geçmişinde öne çıkan 6 gerçek indirimi keşfet.",
            content);
    }
}

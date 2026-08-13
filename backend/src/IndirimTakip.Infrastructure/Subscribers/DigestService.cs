using System.Globalization;
using IndirimTakip.Infrastructure.Deals;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

public record DigestResult(int DealCount, int SubscriberCount);

// Kişiye özel ürün alarmı DEĞİL (bkz. CLAUDE.md) — zamanlanmış tarama
// döngüsünün tespit ettiği en yüksek indirimlerden genel bir özet, tüm
// onaylı abonelere aynı içerikle gönderiliyor. Sadece abonelikten çıkma
// linki her abone için kişiye özel (kendi token'ı).
public class DigestService(AppDbContext db, DealsQueryService dealsQuery, IEmailSender emailSender)
{
    private const int FeaturedDealCount = 6;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<DigestResult> SendDigestAsync(string unsubscribeBaseUrl, CancellationToken cancellationToken = default)
    {
        var deals = await dealsQuery.GetDealsAsync(
            referenceWindowDays: 30, brands: null, categories: null, search: null,
            minPrice: null, maxPrice: null, onlyDiscounted: true, onlyStoreDiscounted: false,
            sortBy: null, page: 1, pageSize: FeaturedDealCount, cancellationToken);

        // Gösterecek gerçek bir indirim yoksa boş/anlamsız bir e-posta göndermek
        // yerine hiç göndermiyoruz.
        if (deals.Items.Count == 0)
            return new DigestResult(0, 0);

        var subscribers = await db.Subscribers
            .Where(s => s.IsConfirmed && s.UnsubscribedAt == null)
            .ToListAsync(cancellationToken);

        var dealsHtml = string.Concat(deals.Items.Select(BuildDealRowHtml));

        foreach (var subscriber in subscribers)
        {
            var unsubscribeUrl = $"{unsubscribeBaseUrl}/api/subscribe/unsubscribe/{subscriber.Token}";
            var html = BuildDigestHtml(dealsHtml, unsubscribeUrl);
            await emailSender.SendAsync(subscriber.Email, "Protein Avcısı — Bu Haftanın Öne Çıkan İndirimleri", html, cancellationToken);
        }

        return new DigestResult(deals.Items.Count, subscribers.Count);
    }

    private static string BuildDealRowHtml(DealDto deal)
    {
        var productUrl = $"https://proteinavcisi.com.tr/urun/{deal.ProductId}";
        var imageHtml = deal.ImageUrl is not null
            ? $"""<img src="{deal.ImageUrl}" alt="" width="72" height="72" style="display:block;border-radius:8px;object-fit:contain;background:#f5f5f4;" />"""
            : """<div style="width:72px;height:72px;border-radius:8px;background:#f5f5f4;"></div>""";
        var referencePrice = deal.ReferencePrice.ToString("N2", TurkishCulture);
        var currentPrice = deal.CurrentPrice.ToString("N2", TurkishCulture);
        var discountPercent = Math.Round(deal.DiscountPercent);

        return $"""
            <tr>
              <td style="padding:12px 0;border-bottom:1px solid #f0efed;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                  <tr>
                    <td width="72" valign="top"><a href="{productUrl}">{imageHtml}</a></td>
                    <td style="padding-left:14px;" valign="top">
                      <a href="{productUrl}" style="color:#1c1917;text-decoration:none;font-size:13px;font-weight:600;line-height:1.4;">{deal.ProductName}</a>
                      <div style="font-size:11px;color:#a8a29e;text-transform:uppercase;margin-top:2px;">{deal.BrandName}</div>
                      <div style="margin-top:6px;">
                        <span style="color:#a8a29e;text-decoration:line-through;font-size:12px;">{referencePrice} TL</span>
                        <span style="color:#059669;font-weight:800;font-size:14px;margin-left:6px;">{currentPrice} TL</span>
                        <span style="background:#f97316;color:#ffffff;font-size:11px;font-weight:700;padding:2px 6px;border-radius:9999px;margin-left:6px;">-%{discountPercent}</span>
                      </div>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
            """;
    }

    // Deal kartları için <table> düzeni bilinçli — e-posta istemcileri arasında
    // (özellikle görsel + metnin yan yana durduğu bu tarz çok-sütunlu
    // yerleşimlerde) en güvenilir sonucu tablo veriyor, flex/grid değil.
    private static string BuildDigestHtml(string dealsHtml, string unsubscribeUrl) => $"""
        <div style="max-width:480px;margin:0 auto;padding:32px 24px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
          <div style="text-align:center;margin-bottom:24px;">
            <span style="display:inline-block;width:36px;height:36px;line-height:36px;border-radius:8px;background:#059669;color:#ffffff;font-weight:800;font-size:14px;text-align:center;vertical-align:middle;">PA</span>
            <span style="font-size:18px;font-weight:700;color:#1c1917;vertical-align:middle;margin-left:8px;">Protein<span style="color:#059669;">Avcısı</span></span>
          </div>
          <div style="background:#ffffff;border:1px solid #e7e5e4;border-radius:16px;padding:24px 20px;">
            <h1 style="font-size:16px;font-weight:800;color:#1c1917;margin:0 0 16px;">Bu Haftanın Öne Çıkan İndirimleri</h1>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
              {dealsHtml}
            </table>
            <div style="text-align:center;margin-top:24px;">
              <a href="https://proteinavcisi.com.tr" style="display:inline-block;background:#059669;color:#ffffff;text-decoration:none;font-weight:600;font-size:13px;padding:10px 24px;border-radius:9999px;">Tüm İndirimleri Gör</a>
            </div>
          </div>
          <p style="font-size:11px;color:#a8a29e;line-height:1.5;text-align:center;margin-top:20px;">
            Fiyatlar ilgili markaların kendi web sitelerinden otomatik olarak toplanmaktadır.<br>
            <a href="{unsubscribeUrl}" style="color:#a8a29e;">Bültenden çık</a>
          </p>
        </div>
        """;
}

using System.Globalization;
using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Subscribers;

// Tarama döngüsünün bir parçası olarak çağrılıyor (bkz. ScrapeIngestionService).
// Sadece o taramada FİYATI DEĞİŞEN ürünler arasında aktif ("Haber Ver" tıklanmış
// ama henüz bildirilmemiş) izleme olan ürünleri kontrol ediyor — 600+ ürünün
// tamamı için değil, sadece gerçekten izlenen küçük bir alt küme için sorgu
// çalıştırıyor.
public class ProductWatchNotifier(AppDbContext db, IEmailSender emailSender)
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
                var html = BuildNotifyHtml(watch.Product!, oldPrice, newPrice);
                await emailSender.SendAsync(watch.Subscriber!.Email, $"{watch.Product!.Name} fiyatı düştü!", html, cancellationToken);
                watch.NotifiedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildNotifyHtml(Product product, decimal oldPrice, decimal newPrice)
    {
        var productUrl = $"https://proteinavcisi.com.tr/urun/{product.Id}";
        var imageHtml = product.ImageUrl is not null
            ? $"""<img src="{product.ImageUrl}" alt="" width="96" height="96" style="display:block;border-radius:8px;object-fit:contain;background:#f5f5f4;margin:0 auto 16px;" />"""
            : "";

        return $"""
            <div style="max-width:480px;margin:0 auto;padding:32px 24px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <div style="text-align:center;margin-bottom:24px;">
                <span style="display:inline-block;width:36px;height:36px;line-height:36px;border-radius:8px;background:#059669;color:#ffffff;font-weight:800;font-size:14px;text-align:center;vertical-align:middle;">PA</span>
                <span style="font-size:18px;font-weight:700;color:#1c1917;vertical-align:middle;margin-left:8px;">Protein<span style="color:#059669;">Avcısı</span></span>
              </div>
              <div style="background:#ffffff;border:1px solid #e7e5e4;border-radius:16px;padding:28px 24px;text-align:center;">
                <h1 style="font-size:16px;font-weight:800;color:#1c1917;margin:0 0 16px;">Takip ettiğin ürünün fiyatı düştü!</h1>
                {imageHtml}
                <p style="font-size:13px;font-weight:600;color:#1c1917;margin:0 0 8px;">{product.Name}</p>
                <p style="margin:0 0 20px;">
                  <span style="color:#a8a29e;text-decoration:line-through;font-size:13px;">{oldPrice.ToString("N2", TurkishCulture)} TL</span>
                  <span style="color:#059669;font-weight:800;font-size:16px;margin-left:8px;">{newPrice.ToString("N2", TurkishCulture)} TL</span>
                </p>
                <a href="{productUrl}" style="display:inline-block;background:#059669;color:#ffffff;text-decoration:none;font-weight:600;font-size:13px;padding:10px 28px;border-radius:9999px;">Ürüne Git</a>
              </div>
              <p style="font-size:11px;color:#a8a29e;line-height:1.5;text-align:center;margin-top:20px;">
                Bu, tek seferlik bir bildirimdir — tekrar haber almak istersen ürün sayfasından yeniden "Haber Ver"e tıklaman yeterli.
              </p>
            </div>
            """;
    }
}

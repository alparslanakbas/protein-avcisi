using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Api.Endpoints;

// Mağazaya yönlendirme (/go). Geriye dönük uyum için duruyor —
// mağaza bağlantısı artık doğrudan DealDto.StoreUrl'den gidiyor.
internal static class StoreRedirectEndpoints
{
    public static void MapStoreRedirectEndpoints(this WebApplication app)
    {
        // Affiliate altyapısı: ürün linkleri buradan geçiyor ki ileride affiliate
        // id eklemek kolay olsun (roadmap adım 7). Şimdilik sadece tıklama sayısını
        // tutuyor, dış siteye 302 ile yönlendiriyor.
        app.MapGet("/go/{productId:int}", async (int productId, HttpContext http, AppDbContext db,
            IOptions<AffiliateOptions> affiliateOptions, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([productId], ct);
            if (product is null)
                return Results.NotFound();

            var brandName = await db.Brands
                .Where(b => b.Id == product.BrandId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);

            // Yönlendirme her zaman yapılıyor, ama tıklama SAYACI arama motoru
            // botlarında artmıyor: bu sayaç markalarla paylaşılan tıklama raporunu
            // besliyor ve bot trafiğiyle şişerse veri doğrudan yanıltıcı olur.
            // İşareti ön yüzdeki yönlendirme katmanı koyuyor (bkz. server.ts) —
            // orada user-agent zaten görülüyor.
            if (http.Request.Headers["X-Bot-Request"] != "1")
            {
                product.ClickCount++;
                await db.SaveChangesAsync(ct);
            }

            // Ortaklık programı olan markalarda adrese takip kodu ekleniyor; marka
            // bunu okuyup satış bize atfediyor. Kodlar yapılandırmadan geliyor
            // (repoya girmiyor), tanımsız markada adres olduğu gibi kalıyor.
            // Bot isteklerinde de eklenmiyor: markanın istatistiğini şişirmemek
            // için, tıklama sayacıyla aynı gerekçe.
            var url = product.Url;
            if (http.Request.Headers["X-Bot-Request"] != "1")
            {
                url = AffiliateLinkBuilder.Apply(url, brandName, affiliateOptions.Value);
            }

            return Results.Redirect(url, permanent: false);
        }).RequireRateLimiting("General");
    }
}

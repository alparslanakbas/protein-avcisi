using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Coupons;

public record CreateCouponRequest(
    string? BrandName,
    string? Seller,
    // Kodu olmayan kampanyalar için boş bırakılabilir; bkz. Coupon.Code.
    string? Code,
    string Description,
    DateTimeOffset? ValidUntil)
{
    public bool HasExactlyOneTarget =>
        !string.IsNullOrWhiteSpace(BrandName) ^ !string.IsNullOrWhiteSpace(Seller);
}

// IsActive dahil — süresi geçen/yanlış çıkan bir kuponu deaktive etmenin
// API üzerinden hiçbir yolu yoktu, sadece doğrudan DB erişimiyle mümkündü.
// ValidUntilTemizle NEDEN AYRI BIR ALAN: bu uçta null "dokunma" demek,
// "boşalt" demek değil. Kod için sorun yok — boş METİN göndermek kodu
// siliyor. Ama tarih alanında boş metin diye bir şey yok, dolayısıyla
// süresi olan bir kuponu tekrar "süresiz" yapmanın hiçbir yolu yoktu.
// Açık bir bayrak, sessizce çalışmayan bir alandan iyidir.
public record UpdateCouponRequest(
    string? Code,
    string? Description,
    DateTimeOffset? ValidUntil,
    bool? IsActive,
    bool? ValidUntilTemizle = null);

public class CouponService(AppDbContext db)
{
    public async Task<IReadOnlyList<CouponDto>> GetActiveCouponsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Coupons
            .Where(c => c.IsActive && (c.ValidUntil == null || c.ValidUntil >= now))
            .OrderBy(c => c.Seller ?? c.Brand!.Name)
            .Select(c => new CouponDto(
                c.Id,
                c.Brand != null ? c.Brand.Name : null,
                c.Seller,
                c.Code,
                c.Description,
                c.ValidUntil,
                c.LastVerifiedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CouponDto?> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.HasExactlyOneTarget)
            throw new ArgumentException("Kupon yalnızca bir markaya veya bir satıcıya bağlanmalıdır.", nameof(request));

        Brand? brand = null;
        if (!string.IsNullOrWhiteSpace(request.BrandName))
        {
            var aranan = request.BrandName.Trim();
            brand = await db.Brands.FirstOrDefaultAsync(b => b.Name == aranan, cancellationToken);

            if (brand is null)
            {
                // BİREBİR EŞLEŞME YETMİYOR. Kupon marka adını ELLE yazarak
                // ekleniyor ve katalogdaki yazım her zaman akılda kalmıyor:
                // 8 Eylül'de "DrSupplement" yazıldı, katalogdaki ad
                // "Dr Supplement" (boşluklu) olduğu için 404 döndü.
                //
                // Aynı sorun tarama tarafında ÇOK ÖNCE çözülmüş: FoldBrandName
                // Türkçe harfleri elle katlıyor, boşluk ve noktayı atıyor,
                // tireyi koruyor. Kendi kopyasını yazmak yerine o kullanılıyor —
                // iki kopya zamanla ayrışır ve "tarama buluyor, kupon bulmuyor"
                // gibi anlaşılmaz bir fark doğardı.
                var katlanmis = ScrapeIngestionService.FoldBrandName(aranan);
                brand = (await db.Brands.ToListAsync(cancellationToken))
                    .FirstOrDefault(b => ScrapeIngestionService.FoldBrandName(b.Name) == katlanmis);
            }

            if (brand is null)
                return null;
        }

        var seller = string.IsNullOrWhiteSpace(request.Seller)
            ? null
            : request.Seller.Trim().ToLowerInvariant();

        var coupon = new Coupon
        {
            BrandId = brand?.Id,
            Seller = seller,
            // Boş dize ile NULL aynı şeyi ifade ediyor ("kod yok"); tek bir
            // biçimde saklanıyor ki arayüz iki ayrı boşluk durumu kontrol etmesin.
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Description = request.Description.Trim(),
            ValidUntil = UtcyeCevir(request.ValidUntil),
            LastVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(coupon, brand?.Name);
    }

    public async Task<CouponDto?> UpdateAsync(int id, UpdateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var coupon = await db.Coupons.Include(c => c.Brand).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (coupon is null)
            return null;

        if (request.Code is not null)
            coupon.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        if (request.Description is not null) coupon.Description = request.Description;
        if (request.ValidUntilTemizle == true)
            coupon.ValidUntil = null;
        else if (request.ValidUntil is not null)
            coupon.ValidUntil = UtcyeCevir(request.ValidUntil);
        if (request.IsActive is not null) coupon.IsActive = request.IsActive.Value;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(coupon, coupon.Brand?.Name);
    }

    /// <summary>Tarihi UTC'ye çevirir.</summary>
    /// <remarks>
    /// <b>ZORUNLU.</b> Npgsql, <c>timestamp with time zone</c> kolonuna
    /// yalnızca offset'i 0 olan bir <see cref="DateTimeOffset"/> yazabiliyor;
    /// Türkiye saatiyle ("+03:00") gelen bir tarih
    /// <c>"only offset 0 (UTC) is supported"</c> ile PATLIYOR ve istek 500
    /// dönüyor. 7 Eylül'de canlıda yaşandı.
    ///
    /// Tuzak, gönderenin biçimine bağlı olduğu için sinsi: tarayıcının
    /// <c>&lt;input type="date"&gt;</c> alanı offset'siz ("2026-12-31")
    /// gönderdiği için panel üzerinden hata GÖRÜNMÜYOR, ama aynı ucu bir
    /// betikten ya da farklı bir istemciden çağırmak patlatıyor.
    /// </remarks>
    private static DateTimeOffset? UtcyeCevir(DateTimeOffset? deger) =>
        deger?.ToUniversalTime();

    private static CouponDto ToDto(Coupon coupon, string? brandName) =>
        new(
            coupon.Id,
            brandName,
            coupon.Seller,
            coupon.Code,
            coupon.Description,
            coupon.ValidUntil,
            coupon.LastVerifiedAt);
}

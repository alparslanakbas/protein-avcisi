using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Deals;

/// <remarks>
/// Ürün sayfasının canonical'ı (DealsQueryService) ve sitemap
/// (CatalogStatsQueryService) AYNI haritayı kullanmak zorunda: ikisi ayrışırsa
/// sitemap'te olmayan bir sayfa kendini asıl gösterir ya da tersi. Bu yüzden
/// iki servis arasında paylaşılan tek kopya burada.
/// </remarks>
internal static class KopyaUrunHaritasi
{
    /// <summary>
    /// AYNI MARKA + AYNI İSİMLİ ürün gruplarında hangisinin "asıl" sayfa
    /// olduğunu belirler ve <c>ikincil ürün Id -> asıl ürün Id</c> haritasını
    /// döndürür. Asıl olan ve gruba girmeyen ürünler haritada YER ALMAZ.
    ///
    /// NEDEN GEREKLİ: markaların kendi siteleri aynı ürünü birden çok adreste
    /// yayınlıyor — eski adres, "copy-of-..." taslağı, sonuna "-1" eklenmiş
    /// tekrar. Her adres bizde ayrı bir ürün satırı oluyor ve sayfaları
    /// birbirinin aynısı çıkıyor. Google bunu KOPYA sayıp kendi standart
    /// sayfasını seçiyor: 1 Eylül'de GSC'de "Kopya, Google kullanıcıdan farklı
    /// bir standart sayfa seçti" doğrulaması 21 sayfada BAŞARISIZ oldu.
    /// Canlıda ölçüldü: 67 grup, 140 ürün, 73 fazladan adres.
    ///
    /// Satırlar SİLİNMİYOR — fiyat geçmişleri duruyor ve bir sonraki taramada
    /// zaten yeniden oluşurlardı (kaynak adresleri hâlâ markanın sitemap'inde).
    /// Yapılan tek şey Google'a hangisinin asıl sayfa olduğunu söylemek:
    /// ikincil olanlar sitemap'e girmiyor ve canonical'ları asıl sayfayı
    /// gösteriyor.
    ///
    /// ASIL SEÇİMİ: fiyat geçmişi en zengin olan (yani en uzun süredir
    /// takip ettiğimiz kayıt); eşitlikte en küçük Id — seçim her çağrıda
    /// AYNI sonucu vermek zorunda, yoksa canonical sayfalar arasında salınır.
    /// </summary>
    public static async Task<Dictionary<int, int>> OlusturAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        // Ürün sayısı birkaç bin; Id/BrandId/Name üçlüsünü belleğe alıp
        // gruplamak, EF'e çevrilmesi zor bir gruplu alt sorgu yazmaktan
        // hem basit hem güvenli.
        var hepsi = await db.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.BrandId, p.Name })
            .ToListAsync(cancellationToken);

        var gruplar = hepsi
            .GroupBy(x => (x.BrandId, x.Name))
            .Where(g => g.Count() > 1)
            .ToList();

        if (gruplar.Count == 0)
            return [];

        var idler = gruplar.SelectMany(g => g.Select(x => x.Id)).ToList();

        var gecmisSayilari = await db.PriceHistories
            .AsNoTracking()
            .Where(h => idler.Contains(h.ProductId))
            .GroupBy(h => h.ProductId)
            .Select(g => new { ProductId = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Adet, cancellationToken);

        var harita = new Dictionary<int, int>();
        foreach (var grup in gruplar)
        {
            var asil = grup
                .OrderByDescending(x => gecmisSayilari.GetValueOrDefault(x.Id))
                .ThenBy(x => x.Id)
                .First();

            foreach (var uye in grup.Where(x => x.Id != asil.Id))
                harita[uye.Id] = asil.Id;
        }

        return harita;
    }

    /// <summary>
    /// Tek bir ürün için <see cref="OlusturAsync"/>'in vereceği cevap: ürün aynı marka + adlı bir
    /// grubun ikincil kaydıysa asıl kaydın kimliği, değilse null. Seçim kuralı aynı (en çok fiyat
    /// geçmişi olan, eşitlikte küçük kimlik); yalnızca o ürünün grubu okunuyor.
    /// </summary>
    /// <remarks>
    /// Ürün sayfası ucu (<c>GetProductByIdAsync</c>) eskiden her istekte bütün haritayı kuruyordu:
    /// bütün ürünler + kopya gruplarındaki ~470 ürünün bütün geçmiş satırlarının sayımı (6 Ekim,
    /// canlıda ~45 ms). Google her ürün sayfasını önbelleksiz istediği için bu her taramada ödeniyordu.
    /// </remarks>
    public static async Task<int?> AsilIdAsync(
        AppDbContext db, int urunId, int markaId, string ad, CancellationToken cancellationToken)
    {
        var grup = await db.Products
            .AsNoTracking()
            .Where(p => p.BrandId == markaId && p.Name == ad)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (grup.Count < 2)
            return null;

        var gecmisSayilari = await db.PriceHistories
            .AsNoTracking()
            .Where(h => grup.Contains(h.ProductId))
            .GroupBy(h => h.ProductId)
            .Select(g => new { ProductId = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Adet, cancellationToken);

        var asil = grup
            .OrderByDescending(id => gecmisSayilari.GetValueOrDefault(id))
            .ThenBy(id => id)
            .First();

        return asil == urunId ? null : asil;
    }
}

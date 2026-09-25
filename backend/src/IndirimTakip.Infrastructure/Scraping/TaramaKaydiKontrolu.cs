using IndirimTakip.Core.Scraping;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Her kaynaktan gelen ürünün yutulmadan önce geçtiği ortak kontrol.
/// </summary>
/// <remarks>
/// <b>NEDEN (güvenlik/mimari incelemesi, 26 Eylül).</b> Aynı korumalar
/// scraper'lara tek tek yazılmıştı ve kaymıştı: altı Shopify scraper'ından
/// beşinde sıfır fiyat koruması vardı, HIQ'da yoktu (Commander'daki yoruma
/// göre sıfır fiyat bir kez sıfıra bölme hatası üretmişti). Takma ad
/// sözlüğünü <c>ResolveBrand</c>'e taşırken izlenen yol: kural scraper'da
/// değil, hepsinin geçtiği tek kapıda.
///
/// <b>BUGÜN HİÇBİR ŞEYİ DEĞİŞTİRMİYOR (ölçüldü).</b> Canlıdaki 5.399 üründe
/// https olmayan adres ya da görsel 0, sıfır/negatif fiyat 0, listede olmayan
/// kategori 0. Kontrol bir kaynağın ileride bozuk veri vermesine karşı.
///
/// Ürün adresi https değilse ürün alınmıyor: site o adrese yönlendiriyor ve
/// sunucu ona istek atıyor. Görsel https değilse yalnızca görsel boşalıyor
/// (tarayıcı https sayfada http görseli zaten yüklemez). Kategori sitenin
/// kodlarından biri değilse boşalıyor ve isimden çıkarım devreye giriyor:
/// ham bir kaynak etiketi hiçbir kategori sayfasında görünmeyen ürün demek.
/// </remarks>
internal static class TaramaKaydiKontrolu
{
    /// <summary>Temizlenmiş kayıt; alınmayacaksa null.</summary>
    public static ScrapedProduct? Temizle(ScrapedProduct urun)
    {
        if (urun.Price <= 0 || !HttpsMi(urun.Url))
            return null;

        var gorsel = urun.ImageUrl is not null && HttpsMi(urun.ImageUrl) ? urun.ImageUrl : null;
        var kategori = urun.Category is not null && ProductAttributeParser.CategorySlugs.Contains(urun.Category)
            ? urun.Category
            : null;

        return gorsel == urun.ImageUrl && kategori == urun.Category
            ? urun
            : urun with { ImageUrl = gorsel, Category = kategori };
    }

    private static bool HttpsMi(string adres) =>
        Uri.TryCreate(adres, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

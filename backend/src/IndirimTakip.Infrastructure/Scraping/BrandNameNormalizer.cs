namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Üretici adlarını tek bir kanonik yazıma çeker.
///
/// NEDEN GEREKLİ: bayiler aynı üreticiyi farklı yazıyor. protein7 "Proteinocean"
/// derken markanın kendi sitesi "ProteinOcean" diyor. İkisi ayrı `Brand` kaydı
/// olunca marka ikiye bölünüyordu ve bu sadece kozmetik bir sorun değildi:
///
///   • İkisi de aynı adrese çözülüyor (`brandSlug` küçük harfe indiriyor), yani
///     sitemap'e TEKRAR EDEN adresler giriyordu — canlıda 13 tane ölçüldü.
///   • `resolveBrandFromSlug` ilk eşleşeni döndürdüğü için iki markadan biri
///     hiçbir zaman açılamıyordu; ürünleri markadan taranamıyordu.
///   • GSC'de zaten açık olan "Kopya, farklı standart sayfa" maddesini
///     besliyordu.
///
/// Bu yüzden normalizasyon TEK BİR YERDE duruyor ve bütün çok markalı
/// kaynaklar buradan geçiyor. Daha önce harita yalnızca Provitamin'in içindeydi;
/// protein7 aynı korumadan yararlanamıyordu.
///
/// KURAL: buraya yalnızca AYNI üreticinin farklı yazımları girer. Benzer isimli
/// FARKLI üreticileri birleştirmek uydurma veri olur.
/// </summary>
public static class BrandNameNormalizer
{
    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Markanın kendi sitesinden gelen yazım kanonik kabul ediliyor:
            // o sayfalar zaten dizine girmiş durumda.
            ["Protein Ocean"] = "ProteinOcean",
            ["Proteinocean"] = "ProteinOcean",
            ["Big Joy"] = "BigJoy",
            ["Bigjoy"] = "BigJoy",
            ["Swiss"] = "Swiss Nutrition",
            ["Trec Nutrition"] = "Trec",
            ["Universal Nutrition"] = "Universal",
            ["Zero Shot"] = "ZeroShot",
            ["Zeroshot"] = "ZeroShot",
            ["Dr Pan"] = "Dr. Pan",
            ["Drpan"] = "Dr. Pan",
            ["Bite More"] = "Bite & More",
            ["Hiq"] = "HIQ",
            ["Ssn"] = "SSN",
        };

    /// <summary>
    /// Kanonik marka adı. Bilinmeyen bir ad olduğu gibi (kırpılmış) döner —
    /// tahmin üretilmez.
    /// </summary>
    public static string Normalize(string brandName)
    {
        var trimmed = brandName.Trim();
        return Aliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }
}

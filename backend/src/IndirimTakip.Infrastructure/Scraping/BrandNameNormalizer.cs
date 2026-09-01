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
            // Kanonik yazım veritabanında DURAN yazım: "Dr Pan". Tersine
            // eşlersek mevcut kaydı düzeltmek yerine ikinci bir marka
            // yaratırdık (slug ikisinde de "dr-pan", yani adres çakışırdı).
            ["Dr. Pan"] = "Dr Pan",
            ["Drpan"] = "Dr Pan",
            ["Bite More"] = "Bite & More",
            // Provitamin kataloğundan geldi (1 Eylül): aynı üretici hem
            // "JUST"/"Just", hem "FA"/"Fa Nutrition" yazımıyla geçiyordu ve
            // ikisi de ayrı marka kaydı yaratmıştı. Kanonik taraf her zaman
            // veritabanında ürünü çok olan yazım.
            // Provitamin aynı ürünü ("Synergy Instant BCAA 650 Gr") İKİ ayrı
            // sayfada listeliyor ve birinde marka "Synergy Nutrition",
            // diğerinde "Synergy" yazıyor. Kullanıcı markanın yabancı bir
            // üretici olduğunu ve ürünün ona ait olduğunu doğruladı.
            ["Synergy"] = "Synergy Nutrition",
            ["JUST"] = "Just",
            ["FA Nutrition"] = "Fa Nutrition",
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

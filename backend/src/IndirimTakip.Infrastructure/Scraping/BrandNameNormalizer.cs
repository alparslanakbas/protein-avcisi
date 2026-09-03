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

            // Fit Çarşı (2 Eylül) — bayi kendi marka etiketlerini Title Case
            // yapıyor ve kısaltmaları bozuyor. Kanonik taraf her zaman
            // VERİTABANINDA duran yazım; doğru karşılıklar bayinin ÜRÜN
            // ADLARINDAN doğrulandı, isme bakıp tahmin edilmedi:
            //   "Konzept" -> ürünleri "Z-Konzept Isolate Whey ..." diyor
            //   "Optimum" -> ürünleri "Optimum Gold Standard Whey ..." diyor
            ["Konzept"] = "Z-Konzept",
            ["Optimum"] = "Optimum Nutrition",
            // Türkçe tuzağı: site "SIS"i tr-TR ile küçültünce "Sıs" oluyor
            // (noktasız ı). Markanın kendi yazımı SiS (Science in Sport),
            // ürün adlarında da öyle geçiyor.
            ["Sıs"] = "SiS",
            ["Sis"] = "SiS",
            ["Tnt"] = "TNT",
            ["Gpn"] = "GPN",
            ["Qnt"] = "QNT",
            ["Biotechusa"] = "BioTech USA",

        // MLA Protein (3 Eylül) — mağaza çok markalı ve schema.org marka
        // adlarını KÜÇÜK HARFLE yazıyor ("mla protein", "detoksfit"). Üçü
        // katalogda ZATEN var (Dr Pan, FitNut, Seedn Grains bayilerden
        // geliyor); birebir aynı yazıma çevrilmezse kopya Brand kaydı
        // oluşurdu — 1 Eylül'de tam bu şekilde kopya markalar oluşmuştu.
        ["mla protein"] = "MLA Protein",
        ["Mla Protein"] = "MLA Protein",
        ["Fitnut"] = "FitNut",
        ["Seed'n Grains"] = "Seedn Grains",
        ["Seed’n Grains"] = "Seedn Grains",
        ["detoksfit"] = "Detoksfit",

        // protein34 (3 Eylül) — DÖRDÜNCÜ BAYİ. Taşıdığı 14 markanın hepsi
        // katalogda ZATEN var; adları birebir aynı yazıma çevrilmezse kopya
        // Brand kaydı oluşurdu.
        ["Bigjoy Sports"] = "BigJoy",
        ["Nuclear"] = "Nuclear Nutrition",
        // Sözlük OrdinalIgnoreCase — Türkçe NOKTALI İ'yi tanımaz, yani
        // "KEVİN LEVRONE" büyük yazımı "Kevin Levrone" anahtarıyla EŞLEŞMEZ.
        // Bu yüzden kaynağın yazdığı hâl birebir anahtar olarak konuluyor.
        ["KEVİN LEVRONE"] = "Kevin Levrone",
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

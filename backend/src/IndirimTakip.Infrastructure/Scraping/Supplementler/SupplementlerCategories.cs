namespace IndirimTakip.Infrastructure.Scraping.Supplementler;

/// <summary>
/// Supplementler.com'un kategori kimlikleri ve bizim slug'ımıza eşlemesi.
/// Kimlikler sitenin kendi site haritasından (<c>/sitemapseo</c>) alındı.
///
/// Giyim, ekipman ve aksesuar kategorileri (fitness giyim, ağırlık kemeri,
/// eldiven, shaker, çanta, anahtarlık, atlama ipi, bileklik) BİLİNÇLİ olarak
/// listede yok — projenin kapsamı spor takviyesi.
///
/// Anlamı birden fazla kategoriye yayılan başlıklar (performans artırıcı,
/// enerji ve dayanıklılık, hazır içecek, bitki tozu gibi) null bırakıldı:
/// kategori isimden çıkarıma gidiyor. Yanlış kategori, kategorisiz kalmaktan
/// kötü.
/// </summary>
internal static class SupplementlerCategories
{
    internal static readonly (string Slug, int Id, string? Category)[] All =
    [
        // Protein
        ("whey-protein", 17, "protein-tozu"),
        ("izole-protein", 26, "protein-tozu"),
        ("kazein-sut-proteini", 18, "protein-tozu"),
        ("kompleks-protein", 19, "protein-tozu"),
        ("bitkisel-protein", 239, "protein-tozu"),
        ("et-proteini", 61, "protein-tozu"),
        ("protein-tozu", 11, "protein-tozu"),

        // Kreatin
        ("kreatin-monohidrat", 27, "kreatin"),
        ("kompleks-kreatin", 28, "kreatin"),
        ("patentli-kreatin-creapure-creavitalis", 413, "kreatin"),
        ("kreatin", 14, "kreatin"),

        // Amino asitler
        ("bcaa", 22, "amino-asitler"),
        ("eaa-esansiyel-amino-asit", 385, "amino-asitler"),
        ("glutamin", 24, "amino-asitler"),
        ("arjinin", 20, "amino-asitler"),
        ("beta-alanin", 238, "amino-asitler"),
        ("sitrulin", 386, "amino-asitler"),
        ("likit-amino-asit", 52, "amino-asitler"),
        ("kompleks-amino-asit", 23, "amino-asitler"),
        ("amino-asit", 2, "amino-asitler"),

        // L-Carnitine & CLA
        ("karnitin-l-carnitine", 38, "l-carnitine-cla"),
        ("cla", 39, "l-carnitine-cla"),

        // Kilo & hacim
        ("hacim-arttirici", 30, "kilo-hacim"),
        ("kilo-aldirici", 29, "kilo-hacim"),
        ("kilo-aldiricilar", 5, "kilo-hacim"),
        ("ogun-tozu", 382, "kilo-hacim"),
        ("karbonhidrat-ve-jel", 45, "kilo-hacim"),

        // Yağ yakıcı
        ("diyet-fat-burner", 4, "yag-yakici"),
        ("termojenik", 40, "yag-yakici"),

        // Vitamin & mineral
        ("sporcu-vitaminleri", 13, "vitamin"),
        ("vitaminler-s", 48, "vitamin"),
        ("omega-3-balik-yaglari", 198, "vitamin"),
        ("magnezyum", 406, "vitamin"),
        ("zma-mineraller", 59, "vitamin"),
        ("kolajen", 402, "vitamin"),
        ("antioksidan-s", 60, "vitamin"),
        ("glukozamin-eklem", 46, "vitamin"),
        ("probiyotik-sindirim", 199, "vitamin"),
        ("hmb", 412, "vitamin"),

        // Atıştırmalıklar
        ("protein-bar", 57, "saglikli-atistirmaliklar"),
        ("proteinli-atistirmaliklar", 171, "saglikli-atistirmaliklar"),
        ("saglikli-atistirmaliklar", 162, "saglikli-atistirmaliklar"),
        ("meyve-enerji-barlari", 168, "saglikli-atistirmaliklar"),
        ("fistik-ezmesi", 164, "saglikli-atistirmaliklar"),
        ("yulaf-musli-ve-granola", 165, "saglikli-atistirmaliklar"),
        ("karabugday-ve-pirinc-patlagi", 408, "saglikli-atistirmaliklar"),

        // Kategorisi belirsiz olanlar — isimden çıkarıma bırakılıyor
        ("performans-arttirici", 3, null),
        ("guc-ve-performans", 43, null),
        ("enerji-ve-dayaniklilik", 44, null),
        ("aol", 21, null),
        ("tribulus", 51, null),
        ("kompleks-tribulus", 50, null),
        ("hazir-icecek", 42, null),
        ("elektrolitler", 395, null),
        ("bitki-tozu-superfoods", 403, null),
        ("hindistan-cevizi-yagi", 405, null),
    ];

    /// <summary>Kategori yolu: /c/{slug}-{id}</summary>
    internal static string Path(string slug, int id) => $"/c/{slug}-{id}";
}

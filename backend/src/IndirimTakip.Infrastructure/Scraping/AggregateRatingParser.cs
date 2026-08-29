using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Ürün sayfasındaki schema.org <c>aggregateRating</c> bloğundan markanın
/// kendi sitesinde gösterdiği yıldız ortalamasını ve yorum sayısını okur.
///
/// Neden tek bir ayrıştırıcı yetiyor: puan verisi olan markaların (HIQ,
/// Torq, Yeşilmarka, Hardline) HEPSİ bu bilgiyi ürün sayfasının JSON-LD
/// işaretlemesinde aynı standart alanlarla veriyor — altyapıları farklı
/// olsa da (Shopify / OpenCart / İkas / OniksSoft) çıktı aynı. Marka başına
/// ayrı bir ayrıştırıcı yazmaya gerek yok.
///
/// Değer bulunamazsa null döner; tahmin üretilmez.
/// </summary>
internal static partial class AggregateRatingParser
{
    // Puan 0-5 aralığının dışındaysa ya farklı bir ölçek kullanılıyordur ya
    // da yanlış bir alan yakalanmıştır — ikisinde de kaydetmemek doğru.
    private const decimal MinRating = 0m;
    private const decimal MaxRating = 5m;

    // Tek bir yorumdan gelen "5 üzerinden 5" bilgisi ortalama değildir.
    // Bu eşiğin altındaki ürünler puansız sayılıyor: sıralamada "2 yorumdan
    // 5.0" ile "278 yorumdan 4.88"i yan yana koymak yanıltıcı olurdu.
    public const int MinimumMeaningfulRatingCount = 3;

    public static (decimal? Value, int? Count) Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return (null, null);

        foreach (Match block in AggregateRatingBlockRegex().Matches(html))
        {
            var (value, count) = ReadBlock(block.Value);
            if (value is not null && count is not null)
                return (value, count);
        }

        return (null, null);
    }

    private static (decimal? Value, int? Count) ReadBlock(string block)
    {
        var valueMatch = RatingValueRegex().Match(block);
        var countMatch = RatingCountRegex().Match(block);
        if (!valueMatch.Success || !countMatch.Success)
            return (null, null);

        // Değer bazı sitelerde sayı ("ratingValue": 4.88), bazılarında
        // tırnak içinde metin ("ratingValue": "4.88") geliyor; regex ikisini
        // de aynı gruba düşürüyor. Ayraç her zaman nokta (JSON), bu yüzden
        // InvariantCulture — makinenin yerel ayarına bırakılırsa Türkçe
        // kültürde "4.88" 488 olarak okunurdu.
        if (!decimal.TryParse(valueMatch.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return (null, null);
        if (!int.TryParse(countMatch.Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            return (null, null);

        if (value < MinRating || value > MaxRating || count <= 0)
            return (null, null);

        return (Math.Round(value, 2), count);
    }

    // Bloğu sınırlamak önemli: aynı sayfada tek tek yorumların kendi
    // "reviewRating" blokları da var ve orada da ratingValue geçiyor.
    // Sadece aggregateRating'in içine bakıyoruz.
    [GeneratedRegex(@"""aggregateRating""\s*:\s*\{[^{}]*\}", RegexOptions.IgnoreCase)]
    private static partial Regex AggregateRatingBlockRegex();

    [GeneratedRegex(@"""ratingValue""\s*:\s*""?(?<value>\d+(?:\.\d+)?)""?", RegexOptions.IgnoreCase)]
    private static partial Regex RatingValueRegex();

    // Siteler ya reviewCount ya ratingCount kullanıyor; ikisi de aynı şeyi
    // ifade ediyor (kaç kişi puanladı).
    [GeneratedRegex(@"""(?:reviewCount|ratingCount)""\s*:\s*""?(?<count>\d+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex RatingCountRegex();
}

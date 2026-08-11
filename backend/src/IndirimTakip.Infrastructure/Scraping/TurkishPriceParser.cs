using System.Globalization;
using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

internal static partial class TurkishPriceParser
{
    // "2.899,00 TL" -> 2899.00 ("." binlik ayraç, "," ondalık ayraç, "TL" boşluklu/boşluksuz gelebiliyor)
    public static decimal Parse(string text)
    {
        var cleaned = text.Replace("TL", "").Trim().Replace(".", "").Replace(',', '.');
        return decimal.Parse(cleaned, CultureInfo.InvariantCulture);
    }

    // Bazı sitelerde eski/yeni fiyat aynı konteynerde, tutarsız span yapısıyla geliyor
    // (örn. eski fiyat span içinde, yeni fiyat düz metin). Güncel fiyat her zaman
    // sonda göründüğü için metindeki SON fiyat deseninin eşleşmesini, varsa bir
    // önceki eşleşmeyi de mağazanın beyan ettiği eski fiyat olarak kullanıyoruz.
    public static (decimal Current, decimal? StoreOldPrice) ParsePricePair(string text)
    {
        var matches = PriceRegex().Matches(text);
        if (matches.Count == 0)
            throw new FormatException($"Metinde fiyat deseni bulunamadı: '{text}'");

        var current = Parse(matches[^1].Value);
        var storeOldPrice = matches.Count >= 2 ? Parse(matches[^2].Value) : (decimal?)null;
        return (current, storeOldPrice);
    }

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})*,\d{2}")]
    private static partial Regex PriceRegex();
}

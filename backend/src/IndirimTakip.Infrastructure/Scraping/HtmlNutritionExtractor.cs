using HtmlAgilityPack;

namespace IndirimTakip.Infrastructure.Scraping;

// SSN, HIQ ve ProteinOcean besin değerlerini klasik bir <table> içinde
// veriyor (Hardline'ınki div tabanlı, o kendi scraper'ında ayrıca ele
// alınıyor). Satırları ham (etiket, değer) çiftlerine çeviren ortak yer —
// normalize etme/filtreleme işi NutritionParser'da.
internal static class HtmlNutritionExtractor
{
    public static IEnumerable<(string Label, string Value)> FromTables(HtmlNode container)
    {
        var tables = container.SelectNodes(".//table");
        if (tables is null)
            yield break;

        foreach (var table in tables)
        {
            foreach (var row in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
            {
                var cells = row.SelectNodes("./td|./th");
                if (cells is null || cells.Count < 2)
                    continue;

                var label = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
                // Son sütun genelde birimi olan asıl değer; aradaki sütunlar
                // (%RDA gibi) NutritionParser tarafından zaten eleniyor.
                var value = HtmlEntity.DeEntitize(cells[1].InnerText).Trim();

                if (label.Length > 0 && value.Length > 0)
                    yield return (label, value);
            }
        }
    }

    // Ham HTML parçası için (ProteinOcean'ın __NEXT_DATA__ içindeki gömülü
    // HTML'i gibi) — önce belgeye çevirip aynı tablo çıkarımını uyguluyor.
    public static IEnumerable<(string Label, string Value)> FromHtmlFragment(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return FromTables(doc.DocumentNode);
    }
}

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
        // "self::table" da dahil — çağıran bir kapsayıcı değil, doğrudan
        // <table> node'unun kendisini verirse de (HIQ'nun scope'lanmış
        // nutrition-table'ı gibi) çalışsın diye.
        var tables = container.SelectNodes(".//table | self::table");
        if (tables is null)
            yield break;

        foreach (var table in tables)
        {
            foreach (var row in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
            {
                var cells = row.SelectNodes("./td|./th");
                if (cells is null || cells.Count < 2)
                    continue;

                // Başlık satırı ("Bileşen | 100 g | 4 g" gibi) veri değil,
                // sütunları etiketliyor — hepsi <th> olduğu için tanınıyor.
                if (cells[0].Name == "th")
                    continue;

                var label = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
                // HIQ gibi markalarda tablo "Bileşen | 100 g | porsiyon"
                // şeklinde 3 sütunlu — ortadaki 100g bazlı değer, SON sütun
                // gerçek porsiyon başına değer. 2 sütunluysa zaten tek değer.
                var value = HtmlEntity.DeEntitize(cells[^1].InnerText).Trim();

                if (label.Length > 0 && value.Length > 0)
                    yield return (label, value);
            }
        }
    }

    // SSN besin değerini HTML <table> olarak değil, tek bir açıklama
    // paragrafının içinde "<strong>Etiket</strong> — değer<br>" satırları
    // olarak veriyor (gerçek bir ürün sayfasında doğrulandı). "—" öncesi
    // <strong> metni etiket, sonrası bir sonraki <strong>/<br>'a kadarki
    // metin değer.
    public static IEnumerable<(string Label, string Value)> FromLabelDashValuePattern(HtmlNode container)
    {
        foreach (var strong in container.SelectNodes(".//strong") ?? Enumerable.Empty<HtmlNode>())
        {
            // "— Şekerler" gibi alt kalem etiketlerindeki öndeki tireyi de temizliyoruz.
            var label = HtmlEntity.DeEntitize(strong.InnerText).TrimStart(' ', '—', '-').Trim();
            if (label.Length == 0)
                continue;

            // <strong> etiketinden sonraki, aynı satırdaki metni (bir sonraki
            // <br>/<strong>'a kadar) topluyor.
            var value = new System.Text.StringBuilder();
            for (var sibling = strong.NextSibling; sibling is not null; sibling = sibling.NextSibling)
            {
                if (sibling.Name is "br" or "strong")
                    break;
                value.Append(HtmlEntity.DeEntitize(sibling.InnerText ?? sibling.OuterHtml));
            }

            var valueText = value.ToString().TrimStart(' ', '—', '-', ':').Trim();
            if (valueText.Length > 0)
                yield return (label, valueText);
        }
    }
}

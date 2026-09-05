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

    /// <summary>
    /// Besin değerini <c>&lt;table&gt;</c> yerine div satırlarıyla veren
    /// kaynaklar için: her satırda İLK çocuk öğe etiketi, SONUNCUSU değeri
    /// taşıyor.
    /// </summary>
    /// <remarks>
    /// <b>Neden gerekti.</b> 5 Eylül'de ölçüldü: katalogda besin değeri olan
    /// ürün 4.918'de 328 (%6,7) ve eksiklerin bir kısmı "kaynakta veri yok"
    /// değil, "kaynak tablo kullanmıyor" yüzündendi. BigJoy'un ürün sayfası
    /// besin değerlerini eksiksiz yayınlıyor (Enerji, Yağ, Karbonhidrat,
    /// Protein…) ama sayfada tek bir <c>&lt;table&gt;</c> yok — hepsi
    /// <c>div.bdegersatir</c> içinde etiket/değer çifti. <see cref="FromTables"/>
    /// bunu göremiyordu.
    ///
    /// Satır seçici ÇAĞIRANDAN geliyor, burada tahmin edilmiyor: "iki çocuğu
    /// olan her div" gibi genel bir kural sayfadaki her yerleşim satırını
    /// besin satırı sanardı.
    /// </remarks>
    public static IEnumerable<(string Label, string Value)> FromRowElements(HtmlNode container, string rowXPath)
    {
        foreach (var row in container.SelectNodes(rowXPath) ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = row.SelectNodes("./*");
            if (cells is null || cells.Count < 2)
                continue;

            var label = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
            var value = HtmlEntity.DeEntitize(cells[^1].InnerText).Trim();

            if (label.Length > 0 && value.Length > 0)
                yield return (label, value);
        }
    }

    /// <summary>
    /// Çok sütunlu besin tabloları: hem "%RDA" sütunlarını eleyip doğru
    /// sütunu seçer, hem de tek hücreye <c>&lt;br&gt;</c> ile sıkıştırılmış
    /// birden çok besini ayırır.
    /// </summary>
    /// <remarks>
    /// <b>NEDEN <see cref="FromTables"/> YETMİYOR.</b> O metot SON sütunu
    /// alıyor ve bu HIQ için doğru karar (orada son sütun porsiyon başına
    /// değer). Muscle Pump'ta ise sütunlar şöyle:
    /// <c>BESİN | 100gr İÇİN | 100gr İÇİN RA* % | 30gr İÇİN | 30gr İÇİN RA* %</c>
    /// — son sütun YÜZDE. Son sütunu almak "Protein: 6" gibi sessizce yanlış
    /// bir değer yazardı; sayı olduğu için hiçbir süzgeç de yakalamazdı.
    /// Kural: başlığında "%" GEÇMEYEN en sağdaki sütun (yani en dar
    /// porsiyon), hepsinde geçiyorsa son sütun.
    ///
    /// <b>İKİNCİ TUZAK — br ile paketlenmiş satırlar.</b> Kaynak iki besini
    /// tek satıra koyabiliyor: etiket <c>YAĞ&lt;br&gt;DOYMUŞ YAĞ</c>, değer
    /// <c>1,32gr&lt;br&gt;0,81gr</c>. Düz metin okumak "YAĞ DOYMUŞ YAĞ =
    /// 1,32gr 0,81gr" üretirdi — değerde sayı olduğu için bu da süzgeçten
    /// geçer ve tabloya saçma bir satır olarak girerdi. Parça sayıları
    /// eşleşiyorsa satır bölünüyor; eşleşmiyorsa BÖLÜNMÜYOR (yanlış
    /// eşleştirmektense birleşik bırak).
    ///
    /// <b>BAŞLIK SATIRI SABİT DEĞİL.</b> <c>&lt;th&gt;</c> olmayabilir
    /// (Muscle Pump'ta <c>&lt;td&gt;&lt;strong&gt;</c>) ve İLK SATIR DA
    /// olmayabilir (Swiss'in tablosu tek hücreli bir ürün başlığıyla
    /// başlıyor). Başlık, en az iki hücresi olan ilk satır kabul ediliyor;
    /// öncesindeki satırlar atlanıyor.
    /// </remarks>
    public static IEnumerable<(string Label, string Value)> FromMultiColumnTable(HtmlNode container)
    {
        var tables = container.SelectNodes(".//table | self::table");
        if (tables is null)
            yield break;

        foreach (var table in tables)
        {
            var rows = table.SelectNodes(".//tr");
            if (rows is null || rows.Count < 2)
                continue;

            // Başlık HER ZAMAN ilk satır değil: Swiss'in tablosu tek hücreli
            // bir ürün başlığıyla başlıyor ("Yüksek Karbonhidratlı Sporcu
            // Gıdası"). İlk satıra bakıp tabloyu atlamak, o tabloyu tamamen
            // kaybetmek demekti — canlıda ölçüldü, makro tablosu düşüyor ve
            // geriye yalnızca değerleri "**" olan enzim tablosu kalıyordu.
            var headerIndex = rows
                .Select((row, index) => (row, index))
                .FirstOrDefault(x => (x.row.SelectNodes("./td|./th")?.Count ?? 0) >= 2)
                .index;

            var headerCells = rows[headerIndex].SelectNodes("./td|./th");
            if (headerCells is null || headerCells.Count < 2)
                continue;

            var targetIndex = SecilecekSutun(headerCells);

            foreach (var row in rows.Skip(headerIndex + 1))
            {
                var cells = row.SelectNodes("./td|./th");
                if (cells is null || cells.Count <= targetIndex)
                    continue;

                var labels = SatirlaraBol(cells[0]);
                var values = SatirlaraBol(cells[targetIndex]);

                // Parça sayıları tutuyorsa besin başına ayrı satır; tutmuyorsa
                // birleşik hâliyle tek satır (yanlış eşleştirme yapma).
                if (labels.Count == values.Count && labels.Count > 1)
                {
                    for (var i = 0; i < labels.Count; i++)
                    {
                        if (labels[i].Length > 0 && values[i].Length > 0)
                            yield return (labels[i], values[i]);
                    }
                    continue;
                }

                var label = string.Join(" ", labels).Trim();
                var value = string.Join(" ", values).Trim();
                if (label.Length > 0 && value.Length > 0)
                    yield return (label, value);
            }
        }
    }

    /// <summary>
    /// <see cref="FromMultiColumnTable"/>'ın SEÇTİĞİ sütunun başlığı.
    /// </summary>
    /// <remarks>
    /// Porsiyon büyüklüğü bazı kaynaklarda ayrı bir alanda değil, tam da bu
    /// başlıkta yazılı ("30gr İÇİN"). Sütun seçme kuralını scraper'a ikinci
    /// kez yazmamak için buradan veriliyor — iki kopya zamanla ayrışır ve
    /// porsiyon yanlış sütundan okunmaya başlardı.
    /// </remarks>
    public static string? MultiColumnPortionHeader(HtmlNode container)
    {
        foreach (var table in container.SelectNodes(".//table | self::table") ?? Enumerable.Empty<HtmlNode>())
        {
            // Başlık satırı ilk satır olmayabilir (bkz. FromMultiColumnTable).
            var headerCells = table.SelectNodes(".//tr")
                ?.Select(row => row.SelectNodes("./td|./th"))
                .FirstOrDefault(cells => (cells?.Count ?? 0) >= 2);
            if (headerCells is null || headerCells.Count < 2)
                continue;

            return HtmlEntity.DeEntitize(headerCells[SecilecekSutun(headerCells)].InnerText)?.Trim();
        }

        return null;
    }

    /// <summary>Başlığında "%" geçmeyen en sağdaki sütun; yoksa son sütun.</summary>
    private static int SecilecekSutun(HtmlNodeCollection headerCells)
    {
        for (var i = headerCells.Count - 1; i >= 1; i--)
        {
            var baslik = HtmlEntity.DeEntitize(headerCells[i].InnerText) ?? string.Empty;
            if (!baslik.Contains('%'))
                return i;
        }

        return headerCells.Count - 1;
    }

    /// <summary>Hücreyi &lt;br&gt; sınırlarından parçalara ayırır.</summary>
    private static List<string> SatirlaraBol(HtmlNode cell)
    {
        var parcalar = new List<string>();
        var tampon = new System.Text.StringBuilder();

        void Bitir()
        {
            var metin = HtmlEntity.DeEntitize(tampon.ToString()).Trim();
            if (metin.Length > 0)
                parcalar.Add(metin);
            tampon.Clear();
        }

        foreach (var node in cell.DescendantsAndSelf())
        {
            if (node.Name == "br")
                Bitir();
            else if (node.NodeType == HtmlNodeType.Text)
                tampon.Append(node.InnerText);
        }

        Bitir();
        return parcalar;
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

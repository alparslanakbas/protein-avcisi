using System.Net;
using System.Text.Json;
using IndirimTakip.Infrastructure.Scraping.Nois;
using IndirimTakip.Infrastructure.Scraping.NutritionLabels;

namespace IndirimTakip.Infrastructure.Tests;

// Metinler SENTETİK DEĞİL: Nois'in gerçek etiket görselleri sunucuda
// tesseract 5.3.4 (tur+eng, psm 6) ile okundu (2026-09-15) ve çıktı olduğu
// gibi kopyalandı — OCR hataları ("2,879", "05g", "O Kcal") dahil.
public class NoisLabelTests
{
    private static Dictionary<string, string> Satirlar(TurkishLabelTextParser.Result r) =>
        r.Rows.ToDictionary(x => x.Label, x => x.Value);

    // Whey Crumble: "g" → "9" ("2,879", "8,8395") ve DV sütunları araya karışmış.
    private const string WheyCrumble = """
        Enerji ve Besin Oğeleri
        Porsiyon Sayısı / Servings : 30 7
        Porsiyon / Serving Size : 1 Ölçek (~30g) /1 scoop (~30g)
        EŞ
        100g toz üründe Porsiyonda (~30g)
        e
        ENERJI 391,4 kcal (1638,71kj)| 117,42 kcal (491,61 kj)
        DV» %DVe*
        Toplam Yag 2,879 3,68% | 0,86g 1,10%
        - Doymuş Yağ 1,76g 8,8395 | 0,53g 2,65»
        Lif Og 09 | Og 0%
        Tuz 0,97g 0,04 | 0,29g 0,01
        Karbonhidrat 15,4g 4,15% | 4,62g 1,24
        - Şeker (Laktoz) 11,4g 22,8% | 3,42g 6,84%
        Protein 76g 160% | 22,8g 48%
        LAVEETKENMADDELER Pe
        BCAA 4:1:1 8.333,33mg 2.500mg
        """;

    [Fact]
    public void PorsiyonSutunuOkunuyorVeKontrollerdenGeciyor()
    {
        var r = TurkishLabelTextParser.Parse(WheyCrumble)!;

        Assert.Equal(30m, r.ServingGrams);
        var s = Satirlar(r);
        Assert.Equal("22,8 g", s["Protein"]);
        Assert.Equal("117,42 kcal", s["Enerji"]);
        Assert.Equal("4,62 g", s["Karbonhidrat"]);
        // "2,879" 100 g değeriydi ("2,87g"); porsiyon 0,86 g ile oranı tutuyor.
        Assert.Equal("0,86 g", s["Yağ"]);
        // BCAA mg satırı makro değil, tabloya girmiyor.
        Assert.DoesNotContain(s.Keys, k => k.Contains("BCAA"));
    }

    // Electrolyte: sütun sırası TERS ("1 Servis (3 g) | 100g") ve "05g" virgülü kaybetmiş.
    [Fact]
    public void TersSutunSirasiBasliktanOkunuyor()
    {
        const string electrolyte = """
            Besin Değerleri 1 Servis (3 g) 100g
            Enerji 8 kJ / 2 kcal 267 kJ / 67 kcal
            Yag Og Og
            - Doymus Yag Og Og
            Karbonhidrat 05g 16,7 g
            - Sekerler Og Og
            Protein Og Og
            Tuz 0,83g 27,/ 9
            Aktif Bileşenler
            Aktif Bileşen 1 Servis (3 g) BRD*
            Sodyum 330 mg —
            """;

        var r = TurkishLabelTextParser.Parse(electrolyte)!;

        var s = Satirlar(r);
        Assert.Equal(3m, r.ServingGrams);
        Assert.Equal("2 kcal", s["Enerji"]);
        Assert.Equal("0,5 g", s["Karbonhidrat"]);
        // "27,/ 9" okunamadı: satır sessizce uydurulmuyor, düşüyor.
        Assert.DoesNotContain("Tuz", s.Keys);
    }

    // BCAA Intra: makrolar miligramla; "O" harfleri sıfır.
    [Fact]
    public void MiligramlaBasilanMakroGrameCevriliyor()
    {
        const string bcaa = """
            Enerji ve Besin Öğeleri
            Porsiyon / Serving Size : 1 Ölçek (~10g) /1 scoop (~10g)
            100g toz üründe Porsiyonda (~10g)
            ENERJİ 50,8 kcal (212,69 ki) 5,08 kcal (21,27 ki)
            % DV** %DV**
            Toplam Yag Og 0% | Og OY
            - Doymuş Yağ Og 0% | Og 0%
            Lif Og 0% | Og 0%
            Tuz 1000mg 43,48% | 100mg 4,35%
            Karbonhidrat 12.772,5mg 4,62% | 1.277,25mg 0,46%
            - Seker 1.151,6mg 2,3% | 115,16mg 0,23
            Protein Og 0 | Og 0%
            """;

        var s = Satirlar(TurkishLabelTextParser.Parse(bcaa)!);

        Assert.Equal("1.277,25 mg", s["Karbonhidrat"]);
        Assert.Equal("100 mg", s["Tuz"]);
        Assert.Equal("5,08 kcal", s["Enerji"]);
    }

    // Glycerol: "O Kcal(Okj)", "O gr"; porsiyon gramı başlığın ÜSTÜNDEKİ satırda.
    [Fact]
    public void SifirKaloriEtiketiVeUstSatirdakiPorsiyon()
    {
        const string glycerol = """
            ENERJİ ve BESİN ÖĞELERİ TABLOSU
            Servis Miktarı :(3 g)
            Toplam Servis Miktarı :40 Servis
            100 g Üründe 1 Servis Üründe % BRD *
            Enerji O Kcal(Okj) O Kcal(Okj)
            Yag O gr O gr
            Karbonhidrat O gr O gr
            Protein O gr O gr
            Giycerol 300 gr 3000 mg
            """;

        var r = TurkishLabelTextParser.Parse(glycerol)!;

        Assert.Equal(3m, r.ServingGrams);
        Assert.Equal("0 g", Satirlar(r)["Protein"]);
    }

    // Vegan Rex: etiketin KENDİSİ tutarsız (100 g'da 1406,06 kcal, porsiyonda 121,84).
    [Fact]
    public void KendiIcindeTutarsizEtiketReddediliyor()
    {
        const string veganRex = """
            Besin Değerleri Tablosu
            Enerji & Besin Degerleri
            Porsiyon Miktarı: 1 Servis = 30 g | Servis Sayısı: 33 Servis
            Besin Ogesi 100 Gigin _1 Porsiyon(30 G)
            Enerji / Energy 1406,06 kcal 121,84 kcal
            Yağ / Fat 7,669 230g
            Doymus Yag / Of Which Saturates 29 069
            Karbonhidrat / Carbohydrate 128g 0,384 g
            Seker / Of Which Sugars 0g Og
            Protein / Protein 83g 24.99
            """;

        Assert.Null(TurkishLabelTextParser.Parse(veganRex));
    }

    // Promeal: başlık "1 Porsiyon(60 G)" ama değerler 100 g'ın tam yarısı.
    [Fact]
    public void BaslikPorsiyonuDegerlerleTutmazsaReddediliyor()
    {
        const string promeal = """
            Porsiyon Miktarı: 1 Servis = 60 g | Servis Sayısı: 12 Servis
            Besin Öğesi 100 G İçin 1 Porsiyon(60 G)
            Enerji 427,05 kcal (1785 kJ) 213,5 kcal (893 kJ)
            Yag 5,59 2,759
            Karbonhidrat 48g 24g
            Protein 44g 22g
            """;

        Assert.Null(TurkishLabelTextParser.Parse(promeal));
    }

    // Asıl güvence: tek bir basamak yanlış okunursa (24 → 74) oran bozulur.
    [Fact]
    public void YanlisOkunanPorsiyonDegeriReddediliyor()
    {
        var bozuk = WheyCrumble.Replace("Protein 76g 160% | 22,8g 48%", "Protein 76g 160% | 72,8g 48%");

        Assert.Null(TurkishLabelTextParser.Parse(bozuk));
    }

    [Fact]
    public void EtkenMaddeTablosuMakroSayilmiyor()
    {
        const string gummy = """
            Besin Değerleri Tablosu
            Porsiyon Miktarı: 2 Gummy | Porsiyon Sayısı: 30
            Aktif Bileşenler 1 Servisteki Miktar BRD*
            L-Teanin 100 mg -
            B6 Vitamini 0,7 mg 50%
            """;

        Assert.Null(TurkishLabelTextParser.Parse(gummy));
    }

    // --- Nois çekicisi ---

    private static string Sayfa(string? besinTablosuHtml) =>
        "<html><body><script id=\"__NEXT_DATA__\" type=\"application/json\">"
        + JsonSerializer.Serialize(new
        {
            props = new
            {
                pageProps = new
                {
                    pageSpecificData = new
                    {
                        description = "<p>Whey protein <b>tozu</b>.</p>",
                        attributes = besinTablosuHtml is null
                            ? Array.Empty<object>()
                            : [new { productAttribute = new { name = "Besin Tablosu", type = "HTML" }, value = besinTablosuHtml }],
                    },
                },
            },
        })
        + "</script></body></html>";

    private const string GorselAdresi = "https://cdn.myikas.com/images/x/etiket/image_1080.webp";

    private static NoisScraper Scraper(string sayfa, INutritionLabelOcr ocr) =>
        new(new HttpClient(new SayfaVeGorselHandler(sayfa)), ocr);

    [Fact]
    public async Task EtiketGorseliOkunupBesinTablosuDoluyor()
    {
        var ocr = new SabitOcr(WheyCrumble);
        var d = await Scraper(Sayfa($"<p><img src=\"{GorselAdresi}\"><br></p>"), ocr)
            .FetchDetailsAsync("https://nois.com/nois-whey-crumble-900");

        Assert.Equal(22.8m, d.ProteinPerServingGrams);
        Assert.Equal(30m, d.ServingSizeGrams);
        Assert.Equal("Whey protein tozu .", d.Description);
        Assert.Equal(GorselAdresi, ocr.OkunanGorsel);
    }

    // Tesseract yoksa boş sonuç DÖNMEMELİ: tamamlama servisi ürünü "bakıldı"
    // diye damgalar ve bir daha denemezdi.
    [Fact]
    public async Task OcrKuruluDegilseHataAtiliyor()
    {
        var scraper = Scraper(Sayfa($"<p><img src=\"{GorselAdresi}\"></p>"), new SabitOcr(WheyCrumble, kurulu: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scraper.FetchDetailsAsync("https://nois.com/x"));
    }

    [Fact]
    public async Task BesinTablosuAlaniYoksaYalnizcaAciklamaGeliyor()
    {
        var ocr = new SabitOcr(WheyCrumble);
        var d = await Scraper(Sayfa(null), ocr).FetchDetailsAsync("https://nois.com/x");

        Assert.Null(d.NutritionJson);
        Assert.NotNull(d.Description);
        Assert.Null(ocr.OkunanGorsel);
    }

    [Fact]
    public async Task OkunamayanEtikettePsm3DeneniyorSonraBosDonuyor()
    {
        var ocr = new SabitOcr("okunamayan metin");
        var d = await Scraper(Sayfa($"<p><img src=\"{GorselAdresi}\"></p>"), ocr).FetchDetailsAsync("https://nois.com/x");

        Assert.Null(d.NutritionJson);
        Assert.Equal([6, 3], ocr.DenenenModlar);
    }

    private sealed class SayfaVeGorselHandler(string sayfa) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.Host == "cdn.myikas.com"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sayfa) });
    }
}

internal sealed class SabitOcr(string? metin, bool kurulu = true) : INutritionLabelOcr
{
    public string? OkunanGorsel { get; private set; }
    public List<int> DenenenModlar { get; } = [];

    public bool IsAvailable => kurulu;

    public Task<string?> ReadAsync(byte[] image, int pageSegmentationMode, CancellationToken cancellationToken)
    {
        OkunanGorsel = "https://cdn.myikas.com/images/x/etiket/image_1080.webp";
        DenenenModlar.Add(pageSegmentationMode);
        return Task.FromResult(metin);
    }
}

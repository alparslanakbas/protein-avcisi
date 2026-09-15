using System.Net;
using IndirimTakip.Infrastructure.Scraping.NutritionLabels;
using IndirimTakip.Infrastructure.Scraping.West;

namespace IndirimTakip.Infrastructure.Tests;

// OCR metinleri SENTETİK DEĞİL: West'in gerçek etiket görselleri canlı backend
// konteynerinde (tesseract 5.3.4, tur+eng) okundu (2026-09-15) ve çıktı olduğu
// gibi kopyalandı.
public class WestLabelTests
{
    private static Dictionary<string, string> Satirlar(TurkishLabelTextParser.Result r) =>
        r.Rows.ToDictionary(x => x.Label, x => x.Value);

    // Whey 2400, büyütmeden okunmuş (psm 6). "2g" → "29", "1,2g" → "1,29",
    // "3,2g" → "3,20"; iki okuma üretilip oranı tutan seçiliyor.
    private const string Whey2400 = """
        Enerji ve Besin Öğeleri 1 Servis 1 Ölçek (30g)
        Supplement Facts 1 Serving size 1 scoops (30g)
        100 g Toz Uriinde Porsiyonda
        Enerji / Calorie 399 kcal (1696 kj) o 120 kcal (510 ki)
        Yağ / Fat 6,8 g 29
        “Doymuş Yağ / Saturated Fat 4g 1,29
        Karbonhidrat / Carbohydrate 10,79 3,20
        “Şeker / Sugar 4,9g 1,59
        Protein / Protein 73,8 g 22
        Tuz / Salt 0,/g 0,2g
        Vitamin B6/ Pridoksin Hidroklorit o 4,66 mg (BRD:%332) o 1,4mg(BRD:90100)
        """;

    [Fact]
    public void IkiOkumaArasindanOraniTutanSeciliyor()
    {
        var r = TurkishLabelTextParser.Parse(Whey2400)!;

        var s = Satirlar(r);
        Assert.Equal(30m, r.ServingGrams);
        Assert.Equal("22 g", s["Protein"]);
        // "29": 29 g ile 6,8 × 0,3 tutmaz, 2 g tutar.
        Assert.Equal("2 g", s["Yağ"]);
        // "4g 1,29": 100 g değeri tam sayı (± 0,5), 4 × 0,3 = 1,2 hem 1,29 hem
        // 1,2 okumasını tutuyor — iki farklı değer, satır belirsiz diye düşüyor.
        // Yakın olanı seçmek tahmin olurdu.
        Assert.DoesNotContain("Doymuş Yağ", s.Keys);
        Assert.Equal("120 kcal", s["Enerji"]);
        // "0,/g" okunamadı: satır uydurulmuyor.
        Assert.DoesNotContain("Tuz", s.Keys);
    }

    // Aynı görsel 2 kat büyütülünce "73,8 g 22" → "13,8 9 22" okundu: 13,8 ×
    // 0,3 = 4,1 ≠ 22. Protein satırı düşüyor, etiket reddediliyor.
    [Fact]
    public void BuyutulmusOkumadakiRakamHatasiReddediliyor()
    {
        var bozuk = Whey2400.Replace("Protein / Protein 73,8 g 22", "Protein / Protein 13,8 9 22");

        Assert.Null(TurkishLabelTextParser.Parse(bozuk));
    }

    // Türkçe satır "(0g)" okunduğunda İngilizce "Serving size (30g)" kullanılıyor.
    [Fact]
    public void TurkcePorsiyonOkunamazsaIngilizceSatirKullaniliyor()
    {
        var r = TurkishLabelTextParser.Parse(Whey2400.Replace("1 Ölçek (30g)", "1 Ölçek (0g)"))!;

        Assert.Equal(30m, r.ServingGrams);
    }

    // BCAA: tek sütun (yalnızca porsiyon). Satır kontrolü yapılamaz → ret.
    [Fact]
    public void TekSutunluEtiketReddediliyor()
    {
        const string bcaa = """
            Enerji ve Besin Öğeleri
            Supplement Facts
            Porsiyon 1 tatlı kaşığı (10g)
            Serving size 1 teaspoon (10g)
            Enerji / Calorie 35 kcal (146 ki)
            Yağ / Fat 0,19
            Karbonhidrat / Carbohydrate 2,59
            Protein / Protein 6g
            """;

        Assert.Null(TurkishLabelTextParser.Parse(bcaa));
    }

    // Aynı satırda FARKLI iki porsiyon değeri oranı tutuyorsa hangisinin doğru
    // olduğu bilinemez: 100 g'da "10 g" (± 0,5 yuvarlama) × 0,3 = 3; porsiyonda
    // "3,09" hem 3,09 hem 3,0 okunabilir ve ikisi de tutuyor. Karbonhidrat
    // zorunlu satır olduğu için etiketin tamamı reddediliyor.
    [Fact]
    public void IkiFarkliTutarliDegerVarsaSatirAlinmiyor()
    {
        var text = Whey2400.Replace("Karbonhidrat / Carbohydrate 10,79 3,20", "Karbonhidrat / Carbohydrate 10 g 3,09");

        Assert.Null(TurkishLabelTextParser.Parse(text));
    }

    // --- West çekicisi ---

    private const string SayfaHtml = """
        <html><body>
        <div class="product-detail-tab-content"><div class="product-detail-tab-row active">
          <p>West Nutrition Whey Protein Tozu &amp; aroma seçenekleri.</p></div></div>
        <img itemprop="image" src="//www.westnutrition.com.tr/idea/hl/90/myassets/products/001/whey-protein-2400.PNG?revision=1">
        <div class="thumb-item col-auto"><img src="//www.westnutrition.com.tr/idea/hl/90/myassets/products/001/west-whey-enerji-besin-ogeleri_min.jpg"></div>
        <div class="benzer"><img class="lazyload" data-src="//www.westnutrition.com.tr/idea/hl/90/myassets/products/488/baska-urun-enerji-besin.jpg"></div>
        </body></html>
        """;

    [Fact]
    public void YalnizcaUrununKendiKlasorundekiEtiketAliniyor()
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(SayfaHtml);

        var adresler = WestNutritionScraper.LabelImageUrls(doc, SayfaHtml);

        // 488 "benzer ürünler" bloğundan: başka ürünün etiketi.
        Assert.Equal(["https://www.westnutrition.com.tr/myassets/products/001/west-whey-enerji-besin-ogeleri.jpg"], adresler);
    }

    [Fact]
    public void AnaGorselYoksaEtiketTahminEdilmiyor()
    {
        const string html = """<div><img src="//x/myassets/products/001/west-enerji-besin.jpg"></div>""";
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        Assert.Empty(WestNutritionScraper.LabelImageUrls(doc, html));
    }

    [Fact]
    public async Task EtiketOkunupAciklamaylaBirlikteDonuyor()
    {
        var istekler = new List<string>();
        var scraper = new WestNutritionScraper(new HttpClient(new KayitHandler(SayfaHtml, istekler)), new SabitOcr(Whey2400));

        var d = await scraper.FetchDetailsAsync("https://www.westnutrition.com.tr/urun/whey");

        Assert.Equal(22m, d.ProteinPerServingGrams);
        Assert.Equal(30m, d.ServingSizeGrams);
        Assert.Equal("West Nutrition Whey Protein Tozu & aroma seçenekleri.", d.Description);
        Assert.DoesNotContain(istekler, u => u.Contains("/488/"));
    }

    [Fact]
    public async Task OcrKuruluDegilseHataAtiliyor()
    {
        var scraper = new WestNutritionScraper(new HttpClient(new KayitHandler(SayfaHtml, [])), new SabitOcr(Whey2400, kurulu: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scraper.FetchDetailsAsync("https://www.westnutrition.com.tr/urun/whey"));
    }

    private sealed class KayitHandler(string sayfa, List<string> istekler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            istekler.Add(request.RequestUri!.ToString());
            return Task.FromResult(request.RequestUri.AbsolutePath.Contains("/myassets/")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sayfa) });
        }
    }
}

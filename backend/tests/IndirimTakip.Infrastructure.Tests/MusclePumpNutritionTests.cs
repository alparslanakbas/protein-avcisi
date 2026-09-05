using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// musclepump.com.tr'deki gerçek bir ürün sayfasından (WPC instant whey,
// 2026-09-05) alınmış tablo. İki tuzağı da barındırdığı için snapshot
// olarak saklanıyor: son sütun YÜZDE, ve bazı satırlar iki besini tek
// hücreye <br> ile sıkıştırıyor.
public class MusclePumpNutritionTests
{
    private const string GercekTablo = """
        <html><body>
        <div class="tab-pane"><div><table border="1">
        <tbody>
        <tr><td><strong>BESİN İÇİERİĞİ</strong></td><td><strong>100gr İÇİN</strong></td><td><strong>100gr İÇİN RA* %</strong></td><td><strong>30gr İÇİN</strong></td><td><strong>30gr İÇİN RA* %</strong></td></tr>
        <tr><td>ENERJİ</td><td>1652KJ / 390 kcal</td><td>20</td><td>496KJ / 117 kcal</td><td>6</td></tr>
        <tr><td>YAĞ<br />DOYMUŞ YAĞ</td><td>4,4gr<br />2,7gr</td><td>6<br />14</td><td>1,32gr<br />0,81gr</td><td>2<br />4</td></tr>
        <tr><td>KARBONHİDRAT<br />ŞEKERLER</td><td>16g<br />10,3gr</td><td>6<br />11</td><td>4,8gr<br />3,09gr</td><td>2<br />3</td></tr>
        <tr><td>LİF</td><td>1,4gr</td><td>6</td><td>0,42gr</td><td>2</td></tr>
        <tr><td>PROTEİN</td><td>71gr</td><td>142</td><td>21,3gr</td><td>43</td></tr>
        </tbody>
        </table></div></div>
        </body></html>
        """;

    private static HtmlNode Kok(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode;
    }

    // ASIL TUZAK: son sütun "30gr İÇİN RA* %" yani yüzde. FromTables son
    // sütunu alıyor (HIQ için doğru) — burada "PROTEİN = 43" yazardı ve
    // değer sayı olduğu için hiçbir süzgeç yakalamazdı.
    [Fact]
    public void YuzdeSutunuDegil_PorsiyonSutunuOkunuyor()
    {
        var satirlar = HtmlNutritionExtractor.FromMultiColumnTable(Kok(GercekTablo)).ToList();
        var tablo = satirlar.ToDictionary(x => x.Label, x => x.Value);

        Assert.Equal("21,3gr", tablo["PROTEİN"]);
        Assert.Equal("496KJ / 117 kcal", tablo["ENERJİ"]);
        Assert.Equal("0,42gr", tablo["LİF"]);
    }

    // İKİNCİ TUZAK: iki besin tek hücrede. Düz okuma "YAĞ DOYMUŞ YAĞ =
    // 1,32gr 0,81gr" üretirdi — değerde sayı olduğu için tabloya girerdi.
    [Fact]
    public void BrIleSikistirilmisSatirlarAyriliyor()
    {
        var tablo = HtmlNutritionExtractor.FromMultiColumnTable(Kok(GercekTablo))
            .ToDictionary(x => x.Label, x => x.Value);

        Assert.Equal("1,32gr", tablo["YAĞ"]);
        Assert.Equal("0,81gr", tablo["DOYMUŞ YAĞ"]);
        Assert.Equal("4,8gr", tablo["KARBONHİDRAT"]);
        Assert.Equal("3,09gr", tablo["ŞEKERLER"]);
    }

    // Porsiyon UYDURULMUYOR: seçilen sütunun başlığında yazıyor.
    [Fact]
    public void PorsiyonBuyuklugu_SutunBasligindanOkunuyor()
    {
        var baslik = HtmlNutritionExtractor.MultiColumnPortionHeader(Kok(GercekTablo));

        Assert.Equal("30gr İÇİN", baslik);
        Assert.Equal(30m, NutritionServingParser.Grams(baslik));
    }

    // Protein satırı yüzde sütunundan okunsaydı 43 g çıkardı — makul
    // aralıkta olduğu için sessizce kabul edilirdi. Doğru değer 21,3.
    [Fact]
    public void ProteinGrami_YuzdeDegil_GercekDeger()
    {
        var json = NutritionParser.BuildNutritionJson(
            HtmlNutritionExtractor.FromMultiColumnTable(Kok(GercekTablo)));

        Assert.Equal(21.3m, NutritionParser.ExtractProteinGrams(json));
    }

    // Parça sayıları tutmuyorsa satır BÖLÜNMEMELİ — yanlış eşleştirmektense
    // birleşik bırakmak yeğ.
    [Fact]
    public void ParcaSayisiTutmuyorsaBolunmuyor()
    {
        const string dengesiz = """
            <html><body><table>
            <tr><td>BESİN</td><td>30gr İÇİN</td></tr>
            <tr><td>YAĞ<br />DOYMUŞ YAĞ</td><td>1,32gr</td></tr>
            </table></body></html>
            """;

        var satirlar = HtmlNutritionExtractor.FromMultiColumnTable(Kok(dengesiz)).ToList();

        Assert.Single(satirlar);
        Assert.Equal("YAĞ DOYMUŞ YAĞ", satirlar[0].Label);
    }


    // SWISS'TEN GELEN TUZAK: başlık satırı İLK satır olmayabilir. Swiss'in
    // makro tablosu tek hücreli bir ürün başlığıyla başlıyor. Kod önce ilk
    // satıra bakıp "2 hücreden az" diye TABLOYU TAMAMEN ATLIYORDU; canlıda
    // makro tablosu düşüyor ve geriye değerleri "**" olan enzim tablosu
    // kalıyordu, yani sonuç sessizce boş dönüyordu.
    [Fact]
    public void BaslikIlkSatirDegilse_TabloAtlanmiyor()
    {
        const string basliklaBaslayan = """
            <html><body><table>
            <tr><td colspan="4">Yüksek Karbonhidratlı Sporcu Gıdası</td></tr>
            <tr><td>Besin Ögeleri</td><td>100 g</td><td>150 g</td><td>%Günlük Değer</td></tr>
            <tr><td>Protein</td><td>20 g</td><td>30 g</td><td>**</td></tr>
            <tr><td>Karbonhidrat</td><td>77 g</td><td>115.5 g</td><td>**</td></tr>
            </table></body></html>
            """;

        var tablo = HtmlNutritionExtractor.FromMultiColumnTable(Kok(basliklaBaslayan))
            .ToDictionary(x => x.Label, x => x.Value);

        // Porsiyon sütunu (150 g) seçilmeli; yüzde sütunu ("**") değil.
        Assert.Equal("30 g", tablo["Protein"]);
        Assert.Equal("115.5 g", tablo["Karbonhidrat"]);
        Assert.DoesNotContain("Besin Ögeleri", tablo.Keys);
        Assert.Equal("150 g", HtmlNutritionExtractor.MultiColumnPortionHeader(Kok(basliklaBaslayan)));
    }

    [Fact]
    public async Task TabloYoksaNullDonuyor()
    {
        var scraper = new IndirimTakip.Infrastructure.Scraping.MusclePump.MusclePumpScraper(
            new HttpClient(new SabitYanitHandler("<html><body><p>Shaker</p></body></html>"))
            {
                BaseAddress = new Uri("https://musclepump.com.tr"),
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                IndirimTakip.Infrastructure.Scraping.MusclePump.MusclePumpScraper>.Instance);

        var d = await scraper.FetchDetailsAsync("https://musclepump.com.tr/x");

        Assert.Null(d.NutritionJson);
        Assert.Null(d.ServingSizeGrams);
    }

    private sealed class SabitYanitHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }
}

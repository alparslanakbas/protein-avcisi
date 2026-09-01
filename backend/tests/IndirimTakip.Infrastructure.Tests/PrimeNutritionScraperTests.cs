using IndirimTakip.Infrastructure.Scraping.PrimeNutrition;

namespace IndirimTakip.Infrastructure.Tests;

// HTML parçaları primenutrition.com.tr'nin gerçek çıktısından alındı
// (2 Eylül 2026) — uydurulmadı.
public class PrimeNutritionScraperTests
{
    // Sitenin fiyat kutusu: önce normal fiyat, sonra "Havale / EFT" indirimi.
    private const string PriceBox = """
        <ul class="list-unstyled price_pr col-6">
          <li><span class="price">1.299,00 TL</span></li>
          <li><small>Havale / EFT</small> <span>1.234,05 TL</span></li>
        </ul>
        """;

    [Fact]
    public void HavaleIndirimiDegil_NormalFiyatiAlir()
    {
        // Son tutarı alsaydık 1.234,05 çıkardı ve her ürünü %5 ucuz
        // gösterirdik — "gerçek indirim" iddiasını doğrudan zedelerdi.
        Assert.Equal(1299.00m, PrimeNutritionScraper.ExtractPrice(PriceBox));
    }

    // Sitenin kendi schema.org bloğu binlik ayracını ondalık sanıyor
    // ("price": "1.3"). Fiyatı oradan DEĞİL sayfadan okuduğumuz için
    // 1000 TL üstü doğru çıkmalı.
    [Fact]
    public void BinlikAyraciniOndalikSanmaz()
    {
        var html = """<ul class="price_pr"><span>2.899,00 TL</span></ul>""";
        Assert.Equal(2899.00m, PrimeNutritionScraper.ExtractPrice(html));
    }

    [Fact]
    public void DortHaneliFiyatiOkur()
    {
        var html = """<div class="price_pr">12.450,00 TL</div>""";
        Assert.Equal(12450.00m, PrimeNutritionScraper.ExtractPrice(html));
    }

    [Fact]
    public void BinlikAyracsizFiyatiOkur()
    {
        var html = """<div class="price_pr">319,00 TL</div>""";
        Assert.Equal(319.00m, PrimeNutritionScraper.ExtractPrice(html));
    }

    // Kategori/blog sayfasında fiyat kutusu HİÇ yok — sitemap onları da
    // listelediği için bu ayrım şart, yoksa kategori sayfaları ürün sanılırdı.
    [Fact]
    public void FiyatKutusuYoksaNullDoner()
    {
        var html = """<html><h1>Amino Asit</h1><div class="urun-liste">499,00 TL</div></html>""";
        Assert.Null(PrimeNutritionScraper.ExtractPrice(html));
    }

    // Kutu bulunamazsa pencere ilerideki BAŞKA bir ürünün fiyatını
    // yakalamamalı — bu yüzden regex penceresi 400 karakterle sınırlı.
    [Fact]
    public void UzaktakiFiyatiKutuyaBaglamaz()
    {
        var html = "<div class=\"price_pr\"></div>" + new string(' ', 600) + "<span>999,00 TL</span>";
        Assert.Null(PrimeNutritionScraper.ExtractPrice(html));
    }

    // "price_pr" sayfada İKİ anlamda geçiyor: gerçek kutunun class'ı ve
    // varyant değiştiren JavaScript'te bir seçici. Tükenmiş ürünlerde SADECE
    // JS'teki var. Kalıp class özniteliğine bağlı olmasaydı JS bloğuna
    // tutunup oradaki ürün dizisinden yanlış fiyat çekerdi.
    [Fact]
    public void JavaScriptIcindekiSeciciyeTutunmaz()
    {
        var html = """
            <script>$('#price_pr').html(''); products[1]={product_id:"6170",price:"250,00 TL"};</script>
            """;
        Assert.Null(PrimeNutritionScraper.ExtractPrice(html));
    }

    // Aynı sayfada hem JS seçici hem gerçek kutu varsa, GERÇEK kutu kazanmalı.
    [Fact]
    public void JsSeciciVarken_GercekKutuyuBulur()
    {
        var html = """
            <script>$('#price_pr').html(''); products[1]={product_id:"6170",price:"250,00 TL"};</script>
            <ul class="list-unstyled price_pr col-6"><span>1.899,00 TL</span></ul>
            """;
        Assert.Equal(1899.00m, PrimeNutritionScraper.ExtractPrice(html));
    }

    // İlk gerçek taramada 57 ürünün 12'si "Cookie &amp; Cream" diye kaydolmuştu;
    // çözülmezse kullanıcıya sitede aynen öyle görünüyor.
    [Fact]
    public void AddakiHtmlVarligiCozulur()
    {
        var html = """<meta property="og:title" content="Prime Nutrition Whey Protein 990 gram Cookie &amp; Cream" />""";
        Assert.Equal("Prime Nutrition Whey Protein 990 gram Cookie & Cream",
            PrimeNutritionScraper.ExtractName(html));
    }

    // Sitenin schema.org bloğu adı kısaltıyor (67 üründe yalnızca 26 farklı
    // ad); og:title tam adı veriyor. Ad buradan okunmalı.
    [Fact]
    public void TamUrunAdiniOkur()
    {
        var html = """<meta property="og:title" content="Prime Nutrition Whey Protein 495 gram Strawberry Cream" />""";
        Assert.Equal("Prime Nutrition Whey Protein 495 gram Strawberry Cream",
            PrimeNutritionScraper.ExtractName(html));
    }

    [Fact]
    public void OgTitleYoksaBosDoner()
    {
        Assert.Equal(string.Empty, PrimeNutritionScraper.ExtractName("<html><body>yok</body></html>"));
    }
}

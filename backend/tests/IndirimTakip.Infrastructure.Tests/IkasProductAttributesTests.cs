using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// ikas sayfaları menü ve öneri listeleriyle BAŞKA ürünlerin verisini de
// taşıyor. Grizzone'da ölçüldü: tek bir ürün sayfasında 83 ayrı besin
// tablosu var ve hepsi menü yükünden geliyor; yalnızca biri sayfanın kendi
// ürününe ait. "Besin geçen ilk değeri al" yaklaşımı BAŞKA bir ürünün
// değerlerini yazardı — gerçek ama yanlış ürünün verisi, uydurmadan beter.
public class IkasProductAttributesTests
{
    // Menüdeki ürün ÖNCE geliyor; doğru okuma yine de pageSpecificData'yı
    // seçmeli.
    private const string SayfaHtml = """
        <html><body>
        <script id="__NEXT_DATA__" type="application/json">
        {"props":{"pageProps":{
          "propValues":[{"propValues":{"menuLinkList":{"value":[{"value":{"productList":{"value":{"data":[
             {"attributes":[{"productAttribute":{"name":"Besin Değerleri","type":"HTML"},"value":"<table><tr><td>Besin</td><td>1 Porsiyon</td></tr><tr><td>Protein</td><td>99 g</td></tr></table>"}]}
          ]}}}}]}}}],
          "pageSpecificData":{"metaData":{"slug":"dogru-urun"},"attributes":[
            {"productAttribute":{"name":"Gram","type":"TEXT"},"value":"303"},
            {"productAttribute":{"name":"Servis Adedi","type":"TEXT"},"value":"60"},
            {"productAttribute":{"name":"Ölçek / Gram","type":"TEXT"},"value":"5"},
            {"productAttribute":{"name":"Besin Değerleri","type":"HTML"},"value":"<table><tr><td>Besin Öğeleri</td><td>1 Porsiyon (5 g)</td></tr><tr><td>Protein</td><td>5 g</td></tr></table>"}
          ]}
        }}}
        </script>
        </body></html>
        """;

    [Fact]
    public void YalnizcaSayfaninKendiUrunuOkunuyor()
    {
        var attributes = IkasProductAttributes.Read(SayfaHtml);

        Assert.Equal(4, attributes.Count);
        Assert.Contains(attributes, a => a.Name == "Servis Adedi");

        // Menüdeki ürünün "99 g" değeri GELMEMELİ.
        var besin = IkasProductAttributes.ValueOf(attributes, "besin");
        Assert.NotNull(besin);
        Assert.Contains("5 g", besin!, StringComparison.Ordinal);
        Assert.DoesNotContain("99 g", besin, StringComparison.Ordinal);
    }

    [Fact]
    public void PorsiyonAlanlariOkunuyor()
    {
        var attributes = IkasProductAttributes.Read(SayfaHtml);

        Assert.Equal("5", IkasProductAttributes.ValueOf(attributes, "ölçek"));
        Assert.Equal("60", IkasProductAttributes.ValueOf(attributes, "servis adedi"));
    }

    // Grizzone'un kataloğu TAMAMEN BÜYÜK HARF yazılabiliyor. .NET'in
    // invariant küçültmesi noktalı İ'yi çevirmediği için OrdinalIgnoreCase
    // ile "besin" aramak "BESİN DEĞERLERİ"ni kaçırırdı.
    [Fact]
    public void BuyukHarfliTurkceAdEslesiyor()
    {
        const string buyukHarf = """
            <html><body><script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"pageSpecificData":{"attributes":[
              {"productAttribute":{"name":"BESİN DEĞERLERİ","type":"HTML"},"value":"<table><tr><td>x</td><td>y</td></tr></table>"}
            ]}}}}
            </script></body></html>
            """;

        var attributes = IkasProductAttributes.Read(buyukHarf);

        Assert.NotNull(IkasProductAttributes.ValueOf(attributes, "besin"));
    }

    [Fact]
    public void NextDataYoksaBosDonuyor()
    {
        Assert.Empty(IkasProductAttributes.Read("<html><body><p>yok</p></body></html>"));
    }

    [Fact]
    public void BozukJsonPatlamiyor()
    {
        const string bozuk = """
            <html><body><script id="__NEXT_DATA__" type="application/json">{"props":</script></body></html>
            """;

        Assert.Empty(IkasProductAttributes.Read(bozuk));
    }
}

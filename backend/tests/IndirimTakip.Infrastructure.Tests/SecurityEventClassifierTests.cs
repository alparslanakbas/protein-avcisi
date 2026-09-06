using IndirimTakip.Infrastructure.Security;

namespace IndirimTakip.Infrastructure.Tests;

// Bu sınıfın iki yönlü hata yapma ihtimali var ve ikisi de sessiz:
// fazla genişse normal trafiği kaydeder (hacim + gereksiz kişisel veri),
// fazla darsa gerçek saldırıyı kaçırır. İkisi de ayrı ayrı sınanıyor.
public class SecurityEventClassifierTests
{
    [Theory]
    [InlineData(429, "rate-limited")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "unauthorized")]
    [InlineData(500, "server-error")]
    [InlineData(502, "server-error")]
    [InlineData(503, "server-error")]
    public void DikkateDegerDurumKodlariKaydediliyor(int kod, string beklenen)
    {
        Assert.Equal(beklenen, SecurityEventClassifier.Classify(kod, "/api/dev/click-report"));
    }

    // EN ÖNEMLİ TEST: 404'lerin çoğu masum (silinmiş ürün, eski bağlantı).
    // Hepsi kaydedilseydi tablo gürültüyle dolar ve panel okunmaz hâle gelirdi.
    [Theory]
    [InlineData("/urun/4304/hardline-whey-3-matrix-base-2300-gr")]
    [InlineData("/marka/bigjoy/protein-tozu")]
    [InlineData("/kategori/kreatin")]
    [InlineData("/")]
    public void MasumDortYuzDortKaydedilmiyor(string yol)
    {
        Assert.Null(SecurityEventClassifier.Classify(404, yol));
    }

    [Theory]
    [InlineData("/wp-admin/setup-config.php")]
    [InlineData("/.env")]
    [InlineData("/vendor/phpunit/phpunit/src/Util/PHP/eval-stdin.php")]
    [InlineData("/.git/config")]
    [InlineData("/phpmyadmin/index.php")]
    [InlineData("/xmlrpc.php")]
    public void BilinenAcikTaramasiYakalaniyor(string yol)
    {
        Assert.Equal("probe", SecurityEventClassifier.Classify(404, yol));
    }

    // Başarılı istekler HİÇ kaydedilmiyor — sayfa görüntülemesi bu tablonun
    // konusu değil.
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(301)]
    [InlineData(304)]
    public void NormalIstekKaydedilmiyor(int kod)
    {
        Assert.Null(SecurityEventClassifier.Classify(kod, "/api/deals"));
    }

    // TÜRKÇE TUZAĞI: karşılaştırma invariant kültürle yapılıyor ve BU DOĞRU
    // olan. Türkçe kültürle küçültülseydi "I" harfi noktasız "ı" olur,
    // ".INI" uzantılı bir tarama eşleşmez ve sessizce kaçardı.
    [Fact]
    public void BuyukHarfliUzanti_TurkceKultureTakilmiyor()
    {
        Assert.True(SecurityEventClassifier.LooksLikeProbe("/CONFIG.INI"));
        Assert.True(SecurityEventClassifier.LooksLikeProbe("/WP-ADMIN/INDEX.PHP"));
    }

    // Ters yön: yolda Türkçe noktalı İ geçmesi çökme ya da yanlış eşleşme
    // üretmemeli. Ürün adları slug'lara giriyor, bu yol gerçekten oluşabilir.
    [Fact]
    public void TurkceKarakterliYolYanlisEslesmiyor()
    {
        Assert.False(SecurityEventClassifier.LooksLikeProbe("/urun/12/BİGJOY-PROTEİN-TOZU"));
        Assert.Null(SecurityEventClassifier.Classify(404, "/marka/İmperium/vitamin"));
    }

    [Fact]
    public void BosYolCokmuyor()
    {
        Assert.False(SecurityEventClassifier.LooksLikeProbe(""));
        Assert.Null(SecurityEventClassifier.Classify(404, ""));
    }
}

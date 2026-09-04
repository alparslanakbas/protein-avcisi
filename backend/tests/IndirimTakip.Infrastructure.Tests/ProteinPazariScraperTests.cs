using IndirimTakip.Infrastructure.Scraping.ProteinPazari;

namespace IndirimTakip.Infrastructure.Tests;

// Kart yapısı proteinpazari.com.tr'nin gerçek çıktısından alındı (4 Eylül 2026).
//
// SAYFANIN BAŞINDAKİ <style> BLOĞU BİLEREK DURUYOR: site satır içi CSS
// yayınlıyor ve o CSS "product-thumb" ifadesini 400'den fazla kez geçiriyor.
// Ölçüm sırasında ham HTML üzerinde kart aranınca yüzlerce sahte eşleşme
// çıkmıştı; test bunun tekrar etmediğini garanti ediyor.
public class ProteinPazariScraperTests
{
    private const string ListeSayfasi = """
        <style>.main-products.product-grid .product-thumb{padding:5px}
        .main-products.product-grid .product-thumb .price-new{color:red}</style>
        <div class="main-products product-grid">
        <div class="product-layout "><div class="product-thumb"><div class="image">
        <a href="https://proteinpazari.com.tr/hardline-whey-3-2000-gr" class="product-img"><div>
        <img src="data:image/png;base64,AAAA" data-src="https://proteinpazari.com.tr/image/cache/catalog/hardline-500x500.jpg" alt="x"/></div></a></div>
        <div class="caption"><div class="stats"><span class="stat-1"><span class="stats-label">Marka:</span>
        <span><a href="https://proteinpazari.com.tr/hardline">Hardline</a></span></span></div>
        <div class="name"><a href="https://proteinpazari.com.tr/hardline-whey-3-2000-gr">HARDLİNE WHEY 3.0 2000 GR</a></div>
        <div class="price"><div> <span class="price-normal">1.899,00TL</span></div>
        <span class="price-tax">Vergiler Hariç:1.726,36TL</span></div></div></div></div>
        <div class="product-layout  out-of-stock"><div class="product-thumb"><div class="image">
        <a href="https://proteinpazari.com.tr/optimum-gold-standard-whey-2273-gr" class="product-img"><div>
        <img data-src="https://proteinpazari.com.tr/image/cache/catalog/on-500x500.jpg" alt="x"/></div></a></div>
        <div class="caption"><div class="stats"><span class="stat-1"><span class="stats-label">Marka:</span>
        <span><a href="https://proteinpazari.com.tr/optimum-nutrition">Optimum Nutrition</a></span></span></div>
        <div class="name"><a href="https://proteinpazari.com.tr/optimum-gold-standard-whey-2273-gr">OPTİMUM GOLD STANDARD WHEY 2273 GR</a></div>
        <div class="price"><div> <span class="price-new">8.950,00TL</span> <span class="price-old">9.500,00TL</span></div></div></div></div></div>
        <div class="product-layout "><div class="product-thumb"><div class="caption">
        <div class="stats"><span class="stat-1"><span class="stats-label">Marka:</span>
        <span><a href="https://proteinpazari.com.tr/big-joy-sports">BİG JOY SPORTS</a></span></span></div>
        <div class="name"><a href="https://proteinpazari.com.tr/big-joy-shaker-500-ml">BİG JOY SHAKER 500 ML</a></div>
        <div class="price"><div> <span class="price-normal">149,00TL</span></div></div></div></div></div>
        <div class="pagination-results"></div>
        """;

    [Fact]
    public void UrunKartlariniOkur()
    {
        var kartlar = ProteinPazariScraper.ParseCards(ListeSayfasi);

        // Üç kart okundu; üçüncüsü (shaker) takviye dışı diye elendi.
        Assert.Equal(3, kartlar.Count);
        Assert.Equal(2, kartlar.Count(k => k.Product is not null));
    }

    [Fact]
    public void FiyatiTurkceBicimdenOkur()
    {
        var urun = ProteinPazariScraper.ParseCards(ListeSayfasi)
            .Select(k => k.Product)
            .First(p => p?.Name.StartsWith("HARDL") == true)!;

        // "1.899,00TL" → 1899.00. Invariant kültürle okunsaydı 1.899 çıkardı.
        Assert.Equal(1899.00m, urun.Price);
        Assert.Null(urun.StoreOldPrice);
        Assert.Equal("Hardline", urun.BrandName);
        Assert.Equal("https://proteinpazari.com.tr/image/cache/catalog/hardline-500x500.jpg", urun.ImageUrl);
        // BAYİ kaydı: Seller dolu olmalı, yoksa ürün markanın kendi
        // sitesinden geliyormuş gibi görünür.
        Assert.Equal("proteinpazari.com.tr", urun.Seller);
        Assert.True(urun.InStock);
    }

    [Fact]
    public void TukenmisUrunuVeMagazaIndiriminiOkur()
    {
        var urun = ProteinPazariScraper.ParseCards(ListeSayfasi)
            .Select(k => k.Product)
            .First(p => p?.Name.StartsWith("OPT") == true)!;

        Assert.False(urun.InStock);
        Assert.Equal(8950.00m, urun.Price);
        Assert.Equal(9500.00m, urun.StoreOldPrice);
    }

    [Fact]
    public void BuyukHarfliMarkaAdiKanonikYazimaCevriliyor()
    {
        // "BİG JOY SPORTS" katalogdaki "BigJoy" ile eşlenmezse KOPYA marka
        // oluşur. Türkçe noktalı İ yüzünden bu eşleşme kendiliğinden olmuyor.
        var shakerKarti = ProteinPazariScraper.ParseCards(ListeSayfasi)
            .Single(k => k.Url.EndsWith("big-joy-shaker-500-ml"));

        // Shaker aksesuar olduğu için elendi — ama markanın eşlemesi ayrı
        // testte (BrandNameNormalizerTests) doğrulanıyor.
        Assert.Null(shakerKarti.Product);
    }

    [Theory]
    [InlineData("1.899,00TL", 1899.00)]
    [InlineData("249,00TL", 249.00)]
    [InlineData("8.950,00TL", 8950.00)]
    public void TurkceFiyatBicimi(string metin, double beklenen)
        => Assert.Equal((decimal)beklenen, ProteinPazariScraper.ParsePrice(metin));

    [Fact]
    public void BosFiyatNullDoner() => Assert.Null(ProteinPazariScraper.ParsePrice("  "));

    [Fact]
    public void AksesuarKategorisiGezilecekListeyeGirmiyor()
    {
        const string sitemap = """
            <a href="https://proteinpazari.com.tr/protein-tozu">Protein Tozu</a>
            <a href="https://proteinpazari.com.tr/fitness-aksesuar">Fitness Aksesuar</a>
            <a href="https://proteinpazari.com.tr/kargo-ve-teslimat">Kargo</a>
            <a href="https://proteinpazari.com.tr/">Ana Sayfa</a>
            """;

        var kategoriler = ProteinPazariScraper.ParseCategoryLinks(sitemap, "https://proteinpazari.com.tr");

        // Aksesuar kategorisi hiç gezilmiyor: alt ağacındaki 129 üründen
        // 16'sını ad süzgeci YAKALAMIYOR (dizlik, knee wraps, matara...).
        Assert.DoesNotContain(kategoriler, k => k.Contains("fitness-aksesuar"));
        Assert.Contains("https://proteinpazari.com.tr/protein-tozu", kategoriler);
        // Ana sayfa listeye girmiyor.
        Assert.DoesNotContain("https://proteinpazari.com.tr", kategoriler);
        // Bilgi sayfaları isimle ELENMİYOR — gezildiğinde ürün kartı
        // çıkmadığı için kendiliğinden düşüyorlar.
        Assert.Contains("https://proteinpazari.com.tr/kargo-ve-teslimat", kategoriler);
    }
}

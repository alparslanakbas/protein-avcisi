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

    // Kartında marka olmayan ürünler (7 Ekim): marka adın başından, yalnız aynı taramanın marka adlarıyla.
    // Sözlük ve adlar canlıdan: bu bayinin 49 marka adı ve "Protein Pazarı"na düşmüş 54 ürün.
    private static readonly string[] BayininMarkalari =
    [
        "Aegis", "Animal Joy", "Applied Nutrition", "Army of One", "Bad Ass", "Basix",
        "BigJoy", "BioTech USA", "Bioxlab", "Bite & More", "Cellucor", "Clean Powders",
        "DY Nutrition", "Dex Supports", "Dymatize", "Effive Nutrition", "Enervit", "Fa Nutrition",
        "GPN", "Grenade", "Hardline", "Herbina", "Jnx Sports", "Kevin Levrone",
        "Kingsize", "Mealjoy", "Monster Energy", "Multipower", "Nuclear Nutrition", "Nutrend",
        "Nutrever", "Olimp", "On The Go", "Optimum Nutrition", "Prime Hydration", "Prime Nutrition",
        "ProteinOcean", "QNT", "Rule One", "S4U Nutrition", "SNCK", "SiS",
        "TNT", "Trec", "Universal", "Vitargo", "Z-Konzept", "ZeroShot",
        "Zoomad Labs",
    ];

    private static readonly string[] MarkasizAdlar =
    [
        "APPLİED NUTRİTİON CRİTİCAL WHEY PROTEİN 900 GR",
        "BIG JOY VITAMINS-OMEGA 3 60 SOFTGELS",
        "BİG JOY ARGİNİNE 120 KAPSÜL",
        "BİG JOY BCAA + GLUTAMINE + CREATINE 480 GR",
        "BİG JOY BEEF&WHEY PROTEİN 1088 GR",
        "BİG JOY BEEF&WHEY PROTEİN 2176 GR",
        "BİG JOY BETA ALANİNE POWDER 300 GR",
        "BİG JOY BİG WHEY GO 2244 GR - 68 PAKET ÇİKOLATA",
        "BİG JOY BİG WHEY GO 495 GR - 15 PAKET",
        "BİG JOY CLABİG 1000 MG 99 KAPSÜL",
        "BİG JOY CİTRULLİNE MALATE 150 GR",
        "BİG JOY CİTRULLİNE MALATE 300 GR",
        "BİG JOY GLUTABİG 150 GR",
        "BİG JOY HYDRO WHEY PROTEIN ÇİKOLATA 476 GR",
        "BİG JOY ISOPRO WHEY İSOLATE 1098 GR",
        "BİG JOY L-CARNİTİNE 1000 ML",
        "BİG JOY PREDATOR 1000 ML",
        "BİG JOY VITAMINS DETOX PLUS 60 KAPSÜL",
        "BİG JOY VİTAMİNS BROMELAIN 60 TABLET",
        "BİG JOY VİTAMİNS MULTIFORM MAGNESIUM COMPLEX 60 TABLET",
        "BİG JOY VİTAMİNS WOMENS BİGBİOTİC 30 KAPSÜL",
        "BİTE MORE PROTEİN PANCAKE (50 GR) - 12 ADET",
        "BİTE MORE PROTEİN PANCAKE (50 GR) - 12 ADET",
        "BİTE MORE PROTEİN PANCAKE (50 GR) - 12 ADET",
        "DANVİTA CRISP BREAD WHEAT CHEESE GARLIC 130 GR",
        "FLAVA L-THEANINE 45 KAPSÜL",
        "HARDLİNE BCAA 4:1:1 ATB6 120 TABLET",
        "HARDLİNE CAFFEİNE 200 MG LİQUİD 20 ADET (30 Ml)",
        "HARDLİNE CREATİNE %100 MİCRONİZED 300 GR",
        "HARDLİNE HİPRO İSOWHEY 1800 GR",
        "HARDLİNE NATURALS COLLAGEN FLEX VİŞNE 330 GR",
        "HARDLİNE NATURALS VİTAMİN D3 K2 60 JEL KAPSÜL",
        "HARDLİNE PROGAİNER 3000 GR",
        "HARDLİNE TRİBULUS TERRESTRİS 100 KAPSÜL",
        "HARDLİNE WHEY 3 MATRİX 4000 GR",
        "KEVIN LEVRONE GOLD CREATINE 120 TABLET",
        "KİNGSİZE NUTRİTİON CREATINE POWDER 1000 GR AROMASIZ",
        "OLİMP ROCKY ATHELETES CREATINE 200 GR LİMONATA",
        "ON THE GO MAGNESIUM NIGHTTIME FORMULA 60 KAPS",
        "ON THE GO PROGEL + CAFFEİNE 60 ML - 24 ADET",
        "PRIME HYDRATION BLUE CHİLL 500 ML TEKLİ",
        "PROTEINOCEAN CREATINE CREAPURE 500 GR",
        "PROTEİNOCEAN CITRULLINE MALATE 300 GR",
        "PROTEİNOCEAN PRE-WORKOUT 12 SHOT",
        "PROTEİNOCEAN PRE-WORKOUT SUPREME GAME DAY 300 GR",
        "PROTEİNOCEAN PSYLLİUM HUSK 180 GR",
        "PROTEİNOCEAN PUMP STIM FREE 300 GR",
        "PROTEİNOCEAN SPREY ZEYTİNYAĞI 150 ML",
        "PROTEİNOCEAN WHEY PROTEİN KARMA KUTU 6X25 GR",
        "PRİME NUTRİTİON CREATİNE 144 GR (6 GR) - 24 ADET",
        "SNCK PROTEİN BAR 55 GR TEKLİ",
        "SNCK PROTEİN BAR 55 GR TEKLİ KARAMEL",
        "TREC CREATİNE %100 CREATİNE MONOHYDRATE 300 GR",
        "Z-KONZEPT ULTİMATE RECOVERY FORMULA PRO 700 GR",
    ];

    [Theory]
    [InlineData("KİNGSİZE NUTRİTİON CREATINE POWDER 1000 GR AROMASIZ", "Kingsize")]
    [InlineData("BİG JOY BEEF&WHEY PROTEİN 1088 GR", "BigJoy")]
    [InlineData("BIG JOY VITAMINS-OMEGA 3 60 SOFTGELS", "BigJoy")]
    [InlineData("PRIME HYDRATION BLUE CHİLL 500 ML TEKLİ", "Prime Hydration")]
    [InlineData("PRİME NUTRİTİON CREATİNE 144 GR (6 GR) - 24 ADET", "Prime Nutrition")]
    [InlineData("BİTE MORE PROTEİN PANCAKE (50 GR) - 12 ADET", "Bite & More")]
    [InlineData("Z-KONZEPT ULTİMATE RECOVERY FORMULA PRO 700 GR", "Z-Konzept")]
    [InlineData("TREC CREATİNE %100 CREATİNE MONOHYDRATE 300 GR", "Trec")]
    [InlineData("HARDLİNE NATURALS VİTAMİN D3 K2 60 JEL KAPSÜL", "Hardline")]
    [InlineData("PROTEINOCEAN CREATINE CREAPURE 500 GR", "ProteinOcean")]
    public void Markasiz_kartta_marka_adin_basindan_bayinin_marka_adlariyla_bulunuyor(string ad, string beklenen) =>
        Assert.Equal(beklenen, ProteinPazariScraper.MarkayiAdindanBul(ad, BayininMarkalari));

    // Tahmin yok: bayinin hiçbir kartında geçmeyen marka, kelimenin yalnız başı ("HARD" ≠ Hardline) ya da adın
    // ortasında geçen marka eşleşmiyor.
    [Theory]
    [InlineData("DANVİTA CRISP BREAD WHEAT CHEESE GARLIC 130 GR")]
    [InlineData("FLAVA L-THEANINE 45 KAPSÜL")]
    [InlineData("HARDCORE WHEY 900 GR")]
    [InlineData("SHAKER HARDLİNE 700 ML")]
    [InlineData("BIGJOYFUL GAINER")]
    public void Bayinin_marka_adlariyla_birebir_baslamayan_ad_markasiz_kaliyor(string ad) =>
        Assert.Null(ProteinPazariScraper.MarkayiAdindanBul(ad, BayininMarkalari));

    [Fact]
    public void Canlidaki_54_markasiz_urunun_52si_eslesiyor_kalan_ikisi_bayinin_tasimadigi_markalar()
    {
        var sonuc = MarkasizAdlar.Select(a => (Ad: a, Marka: ProteinPazariScraper.MarkayiAdindanBul(a, BayininMarkalari))).ToList();

        Assert.Equal(54, sonuc.Count);
        Assert.Equal(["DANVİTA CRISP BREAD WHEAT CHEESE GARLIC 130 GR", "FLAVA L-THEANINE 45 KAPSÜL"],
            sonuc.Where(s => s.Marka is null).Select(s => s.Ad).Order(StringComparer.Ordinal));
        Assert.Equal(20, sonuc.Count(s => s.Marka == "BigJoy"));
        Assert.Equal(9, sonuc.Count(s => s.Marka == "Hardline"));
        Assert.Equal(8, sonuc.Count(s => s.Marka == "ProteinOcean"));
        Assert.Equal(3, sonuc.Count(s => s.Marka == "Bite & More"));
    }
}

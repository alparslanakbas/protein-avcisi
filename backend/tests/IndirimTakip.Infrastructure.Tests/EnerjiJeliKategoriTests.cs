using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Enerji jeli & sporcu içecekleri kategorisi (28 Eylül). Jel, izotonik/sporcu
/// içeceği ve elektrolit ürünleri kategorisizdi ya da adlarındaki tek bir içerik
/// yüzünden beş kategoriye dağılmıştı. Adlar canlı katalogdan alındı.
/// </summary>
public class EnerjiJeliKategoriTests
{
    private const string Kategori = "enerji-jeli-sporcu-icecekleri";

    // Yorumdaki kategori, kural öncesi canlıdaki yeri.
    [Theory]
    [InlineData("1 Adet Energel Caffeine - Enerji Ve Performans Jeli Kahve 40 Gr")]     // pre-workout ("caffeine")
    [InlineData("SİS GO ENERG+CAFFEINE GEL 60 ML DOUBLE ESPRESSO 30 ADET")]            // pre-workout
    [InlineData("VİTARGO ELECTROLYTE 1000 GR")]                                         // kilo-hacim ("vitargo")
    [InlineData("Proteinocean Electrolyte Blend 150gr Vitamin & Mineral Destekleri Proteinocean")] // vitamin
    [InlineData("OLİMP İSO PLUS ISOTONİC DRİNK 700 GR")]
    [InlineData("ISO Drink Powder İzotonik Toz Spor İçeceği 30 Servis 600 g Portakal Aromalı")]
    [InlineData("ON THE GO BODY:FUEL SPORTS DRİNK 1320 GR")]
    [InlineData("ON THE GO ENERGY CHEWS KARIŞIK AROMALI 30 GR GUMMY TEKLİ")]
    [InlineData("Sorb Electrolyte Drink Mix 16 Gr 10 Adet")]
    [InlineData("4x4 Gel Kafein   GOS")]
    public void Jel_ve_sporcu_icecekleri_yeni_kategoride(string ad)
    {
        Assert.Equal(Kategori, ProductAttributeParser.InferCategory(ad));
    }

    /// <summary>
    /// Marka adı ürün tipini söylüyor: marka silinince "hydration" de gidiyordu
    /// ve 13 şişe kategorisiz kalıyordu.
    /// </summary>
    [Fact]
    public void Marka_adi_elektrolit_diyorsa_yeni_kategoride()
    {
        Assert.Equal(Kategori, ProductAttributeParser.InferCategory("PRIME HYDRATION BARCELONA 500 ML TEKLİ", "Prime Hydration"));
    }

    /// <summary>
    /// Kural bütün canlı adlarda çalıştırılınca bulunan benzerler: elektrolitin
    /// yalnızca içerik olduğu amino ürünleri, gliserol (4 Eylül'den beri
    /// pre-workout), bitişik "softjel" kapsülleri ve "energy" markası.
    /// </summary>
    [Theory]
    [InlineData("Amino Hydration - Buzlu Mavi Frambuaz", null, "amino-asitler")]
    [InlineData("PR Nutrition BCAA + Electrolytes Powder 330 Gr", null, "amino-asitler")]
    [InlineData("NOIS GLYCEROL \"HYPER\" - Enerji İçeceği", null, "pre-workout")]
    [InlineData("HIQ Omega 3 Pure 30 Softjel", null, "vitamin")]
    public void Benzerler_yerinde_kaliyor(string ad, string? marka, string beklenen)
    {
        Assert.Equal(beklenen, ProductAttributeParser.InferCategory(ad, marka));
    }

    [Fact]
    public void Energy_markasi_tasinmiyor()
    {
        Assert.NotEqual(Kategori, ProductAttributeParser.InferCategory("MONSTER ENERGY ULTRA RED 500 ML", "Monster Energy"));
    }

    // Soft jel iki kelime yazılınca da kapsül.
    [Fact]
    public void Ayri_yazilan_soft_jel_kapsul_sayiliyor()
    {
        Assert.NotEqual(Kategori, ProductAttributeParser.InferCategory("Omega 3 1000 mg 120 Soft Jel"));
    }
}

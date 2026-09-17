using IndirimTakip.Infrastructure.Catalog;
using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Panelden yazılan değerler, ancak otomatik okumalarla aynı kontrollerden geçerse yayına çıkıyor.
public class ElleUrunVerisiTests
{
    // Canlı bir etiket (ProteinOcean whey, 30 g): 4×24,1 + 4×1,4 + 9×1,9 = 119,3 → 119 kcal.
    private static readonly ElleBesinIstegi Whey = new(30, 119, 24.1m, 1.4m, 1.9m, null);

    [Fact]
    public void Etiket_degerleri_kabul_edilir_ve_sitedeki_yazimla_tabloya_girer()
    {
        var kontrol = ManualProductDataService.Kontrol(Whey);

        Assert.True(kontrol.Kabul, kontrol.RetSebebi);
        Assert.Equal(
            new[] { ("Porsiyon", "30 g"), ("Enerji", "119 kcal"), ("Yağ", "1.9 g"), ("Karbonhidrat", "1.4 g"), ("Protein", "24.1 g") },
            kontrol.Satirlar);
    }

    // 24,1 yerine 241 yazmak.
    [Fact]
    public void Kalori_toplamini_bozan_yazim_hatasi_sebebiyle_reddedilir()
    {
        var kontrol = ManualProductDataService.Kontrol(Whey with { ProteinGram = 241 });

        Assert.False(kontrol.Kabul);
        Assert.False(string.IsNullOrWhiteSpace(kontrol.RetSebebi));
    }

    // AB etiketi: karbonhidrat lifi içermiyor, lif 2 kcal/g. Yulaf bazlı bir bar:
    // 4×10 + 4×20 + 9×8 + 2×6 = 204 kcal. Lif sayılmasaydı beklenen 192 olurdu.
    [Fact]
    public void AB_etiketinde_lif_kaloriye_2_kcal_ekler()
    {
        var bar = new ElleBesinIstegi(50, 204, 10, 20, 8, 6);

        Assert.True(ManualProductDataService.Kontrol(bar).Kabul);
    }

    [Theory]
    [InlineData(null, 24.1, 1.4, 1.9)]
    [InlineData(119.0, null, 1.4, 1.9)]
    [InlineData(119.0, 24.1, null, 1.9)]
    [InlineData(119.0, 24.1, 1.4, null)]
    public void Enerji_ve_uc_makro_birlikte_girilir(double? kalori, double? protein, double? karb, double? yag) =>
        Assert.False(ManualProductDataService.Kontrol(
            new ElleBesinIstegi(30, (decimal?)kalori, (decimal?)protein, (decimal?)karb, (decimal?)yag, null)).Kabul);

    [Fact]
    public void Negatif_deger_reddedilir() =>
        Assert.False(ManualProductDataService.Kontrol(Whey with { LifGram = -1 }).Kabul);

    // --- Takviye tablosu: adlı satırlar, kontrol edilecek kalori yok ---

    private static ElleBesinIstegi Satirlar(decimal? porsiyon, params (string? Ad, decimal? Miktar, string? Birim)[] satirlar) =>
        new(porsiyon, null, null, null, null, null,
            satirlar.Select(s => new ElleBesinSatiri(s.Ad, s.Miktar, s.Birim)).ToList());

    [Fact]
    public void Takviye_tablosu_makrosuz_kabul_edilir_ve_yazilan_sirayi_korur()
    {
        var kontrol = ManualProductDataService.Kontrol(Satirlar(5,
            ("Kreatin Monohidrat", 5, "g"), ("D3 Vitamini", 25, "mcg"), ("A Vitamini", 3000, "iu"), ("Probiyotik", 10, "Milyar CFU")));

        Assert.True(kontrol.Kabul, kontrol.RetSebebi);
        Assert.Equal(
            new[] { ("Porsiyon", "5 g"), ("Kreatin Monohidrat", "5 g"), ("D3 Vitamini", "25 mcg"), ("A Vitamini", "3000 IU"), ("Probiyotik", "10 milyar CFU") },
            kontrol.Satirlar);
    }

    [Fact]
    public void Makrolar_ve_diger_satirlar_kalori_kontrolunden_sonra_birlikte_yayinlanir()
    {
        var kontrol = ManualProductDataService.Kontrol(Whey with { DigerSatirlar = [new("Tuz", 0.21m, "g"), new("B6 Vitamini", 1.4m, "mg")] });

        Assert.True(kontrol.Kabul, kontrol.RetSebebi);
        Assert.Equal(new[] { "Porsiyon", "Enerji", "Yağ", "Karbonhidrat", "Protein", "Tuz", "B6 Vitamini" }, kontrol.Satirlar.Select(s => s.Ad));
    }

    [Fact]
    public void Diger_satirlar_basarisiz_kalori_kontrolunu_ortmez() =>
        Assert.False(ManualProductDataService.Kontrol(Whey with { ProteinGram = 241, DigerSatirlar = [new("Tuz", 0.2m, "g")] }).Kabul);

    [Fact]
    public void Hicbir_sey_girilmezse_reddedilir() =>
        Assert.False(ManualProductDataService.Kontrol(Satirlar(null)).Kabul);

    [Theory]
    [InlineData("", 5.0, "g")]                      // ad yok
    [InlineData("Kafein", null, "mg")]              // miktar yok
    [InlineData("Kafein", 0.0, "mg")]               // sıfır
    [InlineData("Kafein", -200.0, "mg")]            // negatif
    [InlineData("Kafein", 200.0, "gr")]             // listede olmayan birim
    [InlineData("Kafein", 200.0, null)]             // birim yok
    [InlineData("Kafein", 2000000.0, "mcg")]        // fazladan sıfır
    [InlineData("Protein", 25.0, "g")]              // makro serbest satır olarak
    [InlineData("PROTEIN", 25.0, "g")]              // İngilizce büyük harf (tr-TR "proteın" yapardı)
    [InlineData("LİF", 2.0, "g")]                   // Türkçe büyük harf (invariant "lİf" yapardı)
    [InlineData("toplam yağ", 2.0, "g")]            // yaygın yazım
    [InlineData("Kreatin Monohidrat", 50.0, "g")]   // 5 g porsiyondan büyük
    public void Hatali_satir_sebebiyle_reddedilir(string ad, double? miktar, string? birim)
    {
        var kontrol = ManualProductDataService.Kontrol(Satirlar(5, (ad, (decimal?)miktar, birim)));

        Assert.False(kontrol.Kabul);
        Assert.False(string.IsNullOrWhiteSpace(kontrol.RetSebebi));
    }

    [Fact]
    public void Ayni_satir_iki_kez_reddedilir() =>
        Assert.False(ManualProductDataService.Kontrol(Satirlar(null, ("Çinko", 11, "mg"), ("ÇİNKO ", 11, "mg"))).Kabul);

    [Fact]
    public void Satir_adindaki_bosluklar_toparlanir() =>
        Assert.Equal("Beta Alanin", Assert.Single(ManualProductDataService.Kontrol(Satirlar(null, ("  Beta   Alanin ", 3.2m, "g"))).Satirlar).Ad);

    // Panelin açılır listesi ve kategori ucunun kontrolü bu listeye dayanıyor.
    [Fact]
    public void Kategori_kodlari_sitenin_dokuz_kategorisi()
    {
        Assert.Equal(9, ProductAttributeParser.CategorySlugs.Count);
        Assert.Contains("saglikli-atistirmaliklar", ProductAttributeParser.CategorySlugs);
        Assert.Contains("protein-tozu", ProductAttributeParser.CategorySlugs);
    }
}

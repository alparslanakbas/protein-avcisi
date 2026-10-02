using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Infrastructure.Tests;

// Ürün adlarının tamamı canlı katalogdan (29 Eylül). Korumaların her biri,
// ham kilogram sıralamasında listenin başına çıkan gerçek bir yanlış ürüne
// karşılık geliyor.
public class ValuePickRankerTests
{
    private static int _sonId;

    private static ValuePickCandidate Aday(string ad, string? boyut, decimal fiyat, int markaId = 0, bool? stok = true) =>
        new(++_sonId, markaId == 0 ? _sonId + 1000 : markaId, $"Marka {markaId}", ad, boyut, fiyat, stok);

    private static List<string> Adlar(ValuePickRanking sonuc) =>
        sonuc.Picks.Select(p => p.Candidate.ProductName).ToList();

    [Fact]
    public void ProteinTozundaBaskaBicimdekiUrunleriDislar()
    {
        var adaylar = new[]
        {
            Aday("PROTEİNOCEAN CREAM OF RICE 1000 GR ÇİKOLATA", "1000 Gr", 399m),
            Aday("Mass Gainer Karbonhidrat Tozu 6000 gr 60 Servis Çikolata", "6000 Gr", 3049m),
            Aday("NUTREVER PROTEİN PACK(60 GR) - 1 ADET", "60 Gr", 23m),
            Aday("CLEAN POWDERS PROTEİNLİ MAKARNA KIRMIZI MERCİMEK 240 G", "240 Gr", 99m),
            Aday("HARDLİNE COLLAGEN SPORTS 320 GR VİŞNE", "320 Gr", 500m),
            Aday("Hardline Whey 3 Matrix 2300 Gr", "2300 Gr", 2999m),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "protein-tozu", null, 6);

        Assert.Equal(["Hardline Whey 3 Matrix 2300 Gr"], Adlar(sonuc));
        Assert.Equal(1, sonuc.EligibleCount);
    }

    // Kuru çalıştırmanın ikinci turunda protein tozu listesinde kalanlar.
    // ProteinOcean'ın kreatini marka adındaki "protein" yüzünden bu kategoride.
    [Theory]
    [InlineData("PROTEİNOCEAN CREATINE MONOHYDRATE 1000 GR AROMASIZ")]
    [InlineData("PRİME NUTRİTİON PROTEİN PANCAKE 750 GR")]
    [InlineData("Mealjoy Protein Pancake & Waffle 600gr")]
    [InlineData("MEAL JOY PROTEİNLİ ÇABUK ÇORBA 540 GR KREMALI DOMATES")]
    [InlineData("PR Nutrition Recovery Drink Powder 1275 Gr")]
    [InlineData("Clean Powders Plant Based Complete Meal (Öğün Tozu) 600 Gr")]
    [InlineData("Bahs Yüksek Öğün Tozu 1080gr")]
    [InlineData("Protein Pudding Çilek 450 GR")]
    public void ProteinTozuOlmayanUrunleriDislar(string ad)
    {
        var sonuc = ValuePickRanker.Rank([Aday(ad, "1000 Gr", 500m)], "protein-tozu", null, 6);

        Assert.Empty(sonuc.Picks);
    }

    // Aroma adları ürün biçimi sanılmamalı: "Bebe Bisküvi", "Cookies & Cream",
    // "Brownie" ve "Birthday Cake" protein tozlarının aroması.
    [Theory]
    [InlineData("Nois V-rex Vegan Protein Tozu 1000G 33 Servis - Bebe Bisküvi")]
    [InlineData("Whey Protein 2000 Gr Cookies & Cream")]
    [InlineData("Isolate Whey 908 Gr Brownie")]
    [InlineData("PROTEİNOCEAN WHEY PROTEIN 1600GR BIRTHDAY CAKE")]
    [InlineData("Whey Protein Düşük Karbonhidratlı 1000 Gr")]
    [InlineData("Proteinocean Milk Protein 1000 Gr")]
    public void AromaAdlariniBicimSanmaz(string ad)
    {
        var sonuc = ValuePickRanker.Rank([Aday(ad, "1000 Gr", 900m)], "protein-tozu", null, 6);

        Assert.Single(sonuc.Picks);
    }

    [Fact]
    public void StoksuzuDislarBilinmeyenStoguTutar()
    {
        var adaylar = new[]
        {
            Aday("Whey A 1000 Gr", "1000 Gr", 500m, stok: false),
            Aday("Whey B 1000 Gr", "1000 Gr", 600m, stok: null),
            Aday("Whey C 1000 Gr", "1000 Gr", 700m, stok: true),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "protein-tozu", null, 6);

        Assert.Equal(["Whey B 1000 Gr", "Whey C 1000 Gr"], Adlar(sonuc));
    }

    [Fact]
    public void KilogramFiyatinaGoreSiralarVeHerMarkadanBirUrunAlir()
    {
        var adaylar = new[]
        {
            // Marka 1: büyük paket kg'da daha ucuz, o seçilmeli.
            Aday("Whey 1000 Gr", "1000 Gr", 1200m, markaId: 1),
            Aday("Whey 2 Kg", "2 Kg", 2000m, markaId: 1),
            Aday("Isolate 900 Gr", "900 Gr", 1350m, markaId: 2),
            Aday("Whey 5000 Gr", "5000 Gr", 4500m, markaId: 3),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "protein-tozu", null, 6);

        Assert.Equal(["Whey 5000 Gr", "Whey 2 Kg", "Isolate 900 Gr"], Adlar(sonuc));
        Assert.Equal([900m, 1000m, 1500m], sonuc.Picks.Select(p => p.PricePerKg));
        // Her markadan bir ürün seçilmeden ÖNCEKİ sayı.
        Assert.Equal(4, sonuc.EligibleCount);
    }

    [Fact]
    public void KreatinKarisimlariniVePaketleriDislar()
    {
        var adaylar = new[]
        {
            Aday("GOLD Mass Gainer 3000 G + CREATORQ %100 Micronized Creatine", "3000 Gr", 2548m),
            Aday("Kingsize Nutrition Crea Carb 1000 Gr", "1000 Gr", 1099m),
            Aday("KGT  KREATIN GLUTAMIN TAURIN 1000 Gr", "1000 Gr", 1349m),
            Aday("Mass Gainer 3000 gr - Kreatin - L-Arjinin Paketi", "3000 Gr", 3885m),
            Aday("Nois Creatine Hoopla Amino Asit 300G 30 Servis Limonata", "300 Gr", 419m),
            Aday("KİNGSİZE NUTRİTİON CREATINE POWDER 1000 GR AROMASIZ", "1000 Gr", 999m),
            // Marka adındaki "protein" kelimesi karışım sanılmamalı.
            Aday("Proteinocean Creatine 300 Gr", "300 Gr", 450m),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "kreatin", null, 6);

        Assert.Equal(["KİNGSİZE NUTRİTİON CREATINE POWDER 1000 GR AROMASIZ", "Proteinocean Creatine 300 Gr"], Adlar(sonuc));
    }

    // "3000 Kg" yazılmış 3 kilogramlık maltodekstrin 0 TL/kg ile başa çıkıyordu.
    [Fact]
    public void BirimHatasiOlanBoyutuDislar()
    {
        var adaylar = new[]
        {
            Aday("MALTODEXTRIN 3000 KG", "3000 Kg", 999m),
            Aday("Powertech Masstech Mass Gainer 3600 Gr Çilek", "3600 Gr", 1050m),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "kilo-hacim", null, 6);

        Assert.Equal(["Powertech Masstech Mass Gainer 3600 Gr Çilek"], Adlar(sonuc));
    }

    [Fact]
    public void AgirligiBilinmeyenUrunuDislar()
    {
        var adaylar = new[]
        {
            Aday("Creatine 120 Kapsül", "120 Kapsül", 300m),
            Aday("Creatine", null, 300m),
            Aday("Creatine 500 Gr", "500 Gr", 600m),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "kreatin", null, 6);

        Assert.Equal(["Creatine 500 Gr"], Adlar(sonuc));
    }

    [Fact]
    public void IzoleTuruYalnizcaIzoleVeHidrolizeyiTutar()
    {
        var adaylar = new[]
        {
            Aday("WHEY Protein Isolate - Çikolata Aromalı 2000 Gr", "2000 Gr", 3000m),
            Aday("HIQ Hydro Whey 1000 Gr", "1000 Gr", 1800m),
            Aday("Whey Protein İzole 900 Gr", "900 Gr", 1600m),
            Aday("Whey Protein 2000 Gr", "2000 Gr", 1500m),
            // Konsantre de taşıyan karışım laktozsuz listesine girmemeli.
            Aday("Kompleks Protein Tozu - Whey Complex İzole & Konsantre 66 Servis 1980 gr", "1980 Gr", 3000m),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "protein-tozu", "izole", 6);

        Assert.Equal(3, sonuc.Picks.Count);
        Assert.DoesNotContain("Whey Protein 2000 Gr", Adlar(sonuc));
        Assert.DoesNotContain(sonuc.Picks, p => p.Candidate.ProductName.Contains("Konsantre"));
    }

    // Tek listede kg fiyatına göre ilk altının tamamı pirinç unu ve
    // maltodekstrindi; gainer'lar ayrı listeleniyor. Bayinin ada eklediği
    // "Karbonhidrat Tozu" bir gainer'ı karbonhidrat listesine taşımamalı.
    [Fact]
    public void KiloHacimGainerVeKarbonhidratiAyirir()
    {
        var adaylar = new[]
        {
            Aday("Mealjoy Cream of Rice 1000gr", "1000 Gr", 296m),
            Aday("Saf Aromasız Maltodekstrin 3000 gr 125 Servis", "3000 Gr", 1049m),
            Aday("Bodymax Promass Mass Gainer 3600 Gr 30 Servis", "3600 Gr", 1799m),
            Aday("HIQ Gain Deluxe 5 kg", "5 Kg", 3599m),
            Aday("Optimum Serious Mass Karbonhidrat Tozu 5450gr Kilo Optimum Nutrition", "5450 Gr", 6320m),
        };

        var gainer = Adlar(ValuePickRanker.Rank(adaylar, "kilo-hacim", "gainer", 6));
        var karbonhidrat = Adlar(ValuePickRanker.Rank(adaylar, "kilo-hacim", "karbonhidrat", 6));

        Assert.Equal(
            ["Bodymax Promass Mass Gainer 3600 Gr 30 Servis", "HIQ Gain Deluxe 5 kg", "Optimum Serious Mass Karbonhidrat Tozu 5450gr Kilo Optimum Nutrition"],
            gainer);
        Assert.Equal(["Mealjoy Cream of Rice 1000gr", "Saf Aromasız Maltodekstrin 3000 gr 125 Servis"], karbonhidrat);
    }

    [Theory]
    [InlineData("protein-tozu", "izole", true)]
    [InlineData("protein-tozu", "bitkisel", true)]
    [InlineData("kilo-hacim", "gainer", true)]
    [InlineData("kilo-hacim", "karbonhidrat", true)]
    [InlineData("kreatin", "izole", false)]
    [InlineData("protein-tozu", "gainer", false)]
    [InlineData("protein-tozu", "IZOLE", false)]
    public void TurKategoriyeGoreDogrulanir(string kategori, string tur, bool beklenen)
    {
        Assert.Equal(beklenen, ValuePickRanker.IsValidType(kategori, tur));
    }

    [Fact]
    public void BitkiselTuruYalnizcaBitkiselUrunleriTutar()
    {
        var adaylar = new[]
        {
            Aday("Nois V-rex Vegan Protein Tozu 1000G 33 Servis - Bebe Bisküvi", "1000 Gr", 549m),
            Aday("Bezelye Proteini 1000 Gr", "1000 Gr", 700m),
            Aday("Whey Protein 2000 Gr", "2000 Gr", 1500m),
        };

        var sonuc = ValuePickRanker.Rank(adaylar, "protein-tozu", "bitkisel", 6);

        Assert.Equal(2, sonuc.Picks.Count);
        Assert.DoesNotContain("Whey Protein 2000 Gr", Adlar(sonuc));
    }

    [Fact]
    public void AdetSinirlaniyor()
    {
        var adaylar = Enumerable.Range(1, 20).Select(i => Aday($"Whey {i}", "1000 Gr", 500m + i)).ToList();

        Assert.Equal(3, ValuePickRanker.Rank(adaylar, "protein-tozu", null, 3).Picks.Count);
        Assert.Equal(ValuePickRanker.MaxCount, ValuePickRanker.Rank(adaylar, "protein-tozu", null, 99).Picks.Count);
    }
}

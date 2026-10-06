using IndirimTakip.Api.Endpoints;
using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Ürün sayfası ana listeyi de render ettiği için vitrin ve kart sparkline'ları
/// sayfaya gömülen aktarım verisine biniyor (6 Ekim: HTML'in %86'sı). İki yanıt
/// hafifletildi; bu testler hafifletmenin ekranda hiçbir şeyi değiştirmediğini sınıyor.
/// </summary>
public class UrunSayfasiAktarimVerisiTests
{
    private static readonly DateTimeOffset Baslangic = new(2026, 9, 6, 2, 0, 0, TimeSpan.Zero);

    private static List<PricePointDto> Seri(params decimal[] fiyatlar) =>
        fiyatlar.Select((f, i) => new PricePointDto(f, Baslangic.AddHours(6 * i))).ToList();

    [Fact]
    public void Ayni_fiyatli_kosunun_yalniz_ilk_ve_son_noktasi_kalir()
    {
        var seri = Seri(100, 100, 100, 100, 100);

        var sonuc = PriceHistoryQueryService.KosuSinirlari(seri);

        Assert.Equal(new[] { seri[0], seri[4] }, sonuc);
    }

    [Fact]
    public void Fiyat_degisince_iki_kosunun_sinirlari_kalir()
    {
        var seri = Seri(100, 100, 100, 80, 80, 80);

        var sonuc = PriceHistoryQueryService.KosuSinirlari(seri);

        Assert.Equal(new[] { seri[0], seri[2], seri[3], seri[5] }, sonuc);
    }

    [Fact]
    public void Ardisik_farkli_fiyatlar_ve_kisa_seriler_oldugu_gibi_kalir()
    {
        var farkli = Seri(1, 2, 3, 4);
        Assert.Equal(farkli, PriceHistoryQueryService.KosuSinirlari(farkli));

        var tek = Seri(5);
        Assert.Equal(tek, PriceHistoryQueryService.KosuSinirlari(tek));

        Assert.Empty(PriceHistoryQueryService.KosuSinirlari([]));
    }

    // Çizimin değişmediğinin kanıtı: atılan her nokta, kalan komşularıyla aynı
    // fiyatta, yani iki komşuyu birleştiren yatay çizginin üstünde.
    [Fact]
    public void Atilan_her_nokta_kalan_iki_komsusuyla_ayni_fiyatta()
    {
        var fiyatlar = Enumerable.Repeat(100m, 30)
            .Append(95m)
            .Concat(Enumerable.Repeat(100m, 20))
            .Concat(Enumerable.Repeat(90m, 40))
            .Concat([92m, 91m])
            .Concat(Enumerable.Repeat(90m, 46))
            .ToArray();
        var seri = Seri(fiyatlar);

        var sonuc = PriceHistoryQueryService.KosuSinirlari(seri);

        Assert.Equal(seri[0], sonuc[0]);
        Assert.Equal(seri[^1], sonuc[^1]);
        Assert.True(sonuc.Count < seri.Count / 10, $"{seri.Count} noktadan {sonuc.Count} kaldı");
        foreach (var nokta in seri.Except(sonuc))
        {
            var once = sonuc.Last(k => k.ScrapedAt < nokta.ScrapedAt);
            var sonra = sonuc.First(k => k.ScrapedAt > nokta.ScrapedAt);
            Assert.Equal(nokta.Price, once.Price);
            Assert.Equal(nokta.Price, sonra.Price);
        }
    }

    [Fact]
    public void Vitrin_yaniti_aciklama_ve_besin_tablosunu_tasimaz_gerisi_ayni_kalir()
    {
        var deal = new DealDto(
            ProductId: 4778, ProductName: "HIQ High Pro+ 2 Kg", ProductUrl: "https://example.com/p",
            ImageUrl: "/images/4778.webp", Category: "protein-tozu", Size: "2 Kg", Flavor: "Çikolata",
            ServingSizeGrams: 30, ServingsPerPackage: 66, Description: new string('a', 26_000),
            NutritionJson: "{\"protein\":24}", ProteinPerServingGrams: 24, BrandName: "HIQ",
            CurrentPrice: 1899, ReferencePrice: 2099, DiscountPercent: 9.5m, StoreOldPrice: null,
            StoreDiscountPercent: null, ScrapedAt: Baslangic, IsAtThirtyDayLow: true,
            RatingValue: 4.9m, RatingCount: 3022);

        var vitrin = DealsEndpoints.VitrinIcin(deal);

        Assert.Null(vitrin.Description);
        Assert.Null(vitrin.NutritionJson);
        Assert.Equal(deal with { Description = null, NutritionJson = null }, vitrin);
    }
}

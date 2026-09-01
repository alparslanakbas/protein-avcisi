using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Arama metni, veritabanının `lower()` çıktısıyla AYNI biçime indirgenmek
/// zorunda. Aksi halde eşleşme sessizce boş döner — hata vermez, sadece
/// "sonuç yok" der ve bunu fark etmek zordur.
/// </summary>
public class AramaNormalizasyonTests
{
    [Theory]
    // REGRESYON: .NET'te ToLower() invariant kültürde noktalı İ'yi HİÇ
    // küçültmüyor; "VİTAMİN" araması "vİtamİn" olup veritabanındaki
    // "vitamin" ile asla eşleşmiyordu. Canlıda ölçüldü: "vitamin" 260
    // sonuç verirken "VİTAMİN" 0 veriyordu.
    [InlineData("VİTAMİN", "vitamin")]
    [InlineData("vitamin", "vitamin")]
    [InlineData("VITAMIN", "vitamin")]
    [InlineData("Vitamin", "vitamin")]
    [InlineData("KREATİN", "kreatin")]
    public void BuyukHarfliTurkceYazimAyniSonucaIner(string girdi, string beklenen)
    {
        Assert.Equal(beklenen, DealsQueryService.NormalizeSearchText(girdi));
    }

    [Theory]
    // Noktasız ı da i'ye katlanıyor: Postgres "FISTIK"i "fistik" yapıyor ama
    // "Fıstık"ı "fıstık" bırakıyor. İki taraf da katlanmazsa aynı ürünün iki
    // yazımı birbirini bulmuyordu (canlıda "fıstık" 24, "FISTIK" 6 veriyordu).
    [InlineData("fıstık", "fistik")]
    [InlineData("FISTIK", "fistik")]
    [InlineData("Fıstık", "fistik")]
    public void NoktasizIveNoktaliIAyniHarfeIner(string girdi, string beklenen)
    {
        Assert.Equal(beklenen, DealsQueryService.NormalizeSearchText(girdi));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BosGirdiBosDonerAramaCalismaz(string? girdi)
    {
        Assert.Equal(string.Empty, DealsQueryService.NormalizeSearchText(girdi));
    }

    [Fact]
    public void BastakiVeSondakiBosluklarKirpilir()
    {
        Assert.Equal("torq protein", DealsQueryService.NormalizeSearchText("  Torq Protein  "));
    }

    [Fact]
    public void KelimeArasiBosluklarKORUNUR()
    {
        // Kelime bazlı arama bu boşluklara dayanıyor; sıkıştırılırsa
        // "Torq Protein" tek kelimeye dönüşür ve hata geri gelir.
        Assert.Contains(' ', DealsQueryService.NormalizeSearchText("Torq Protein"));
    }
}

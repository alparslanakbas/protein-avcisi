using IndirimTakip.Api.Endpoints;
using Microsoft.Extensions.Configuration;

namespace IndirimTakip.Infrastructure.Tests;

// Sağlık ucunun alarm kararı ve süreli susturma. Sessizce yeşil kalan bir
// alarm, kırmızı kalan bir alarmdan daha tehlikeli: testlerin çoğu "susturma
// fazlasını susturmuyor" yönünü sınıyor.
public class SaglikSusturmaTests
{
    private static readonly DateTimeOffset Simdi = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);

    private static HealthEndpoints.KaynakDurumu Kaynak(string ad, double saatOnce) =>
        new(ad, Simdi.AddHours(-saatOnce), 10);

    private static IReadOnlyList<HealthEndpoints.KaynakSusturma> Oku(params (string Anahtar, string Deger)[] ayarlar) =>
        HealthEndpoints.SusturmalariOku(new ConfigurationBuilder()
            .AddInMemoryCollection(ayarlar.ToDictionary(a => a.Anahtar, a => (string?)a.Deger))
            .Build());

    private static readonly HealthEndpoints.KaynakSusturma[] GigisSusturuldu =
        [new("Gigi's", new DateOnly(2026, 10, 10))];

    // 2 Ekim gecesinin durumu: Gigi's bilinen arıza, Bahs yeni bozulan kaynak.
    // Susturma olmasaydı Bahs'ın bozulması, zaten "down" olan monitörde görünmezdi.
    [Fact]
    public void SusturulanKaynak503UretmezBaskaBayatKaynakUretir()
    {
        var sonuc = HealthEndpoints.Siniflandir(
            [Kaynak("Gigi's", 36), Kaynak("Bahs", 30), Kaynak("HIQ", 5)], Simdi, 26, GigisSusturuldu);

        Assert.Equal(["Bahs"], sonuc.Bayat.Select(k => k.Kaynak));
        var susturulan = Assert.Single(sonuc.Susturulan);
        Assert.Equal("Gigi's", susturulan.Kaynak.Kaynak);
        Assert.Equal(new DateOnly(2026, 10, 10), susturulan.Bitis);
    }

    // Bitiş günü dahil (UTC); ertesi gün kaynak yeniden alarm üretiyor. Süresiz
    // susturma yok: unutulan bir kayıt kaynağı sessizce izlemeden çıkarmasın.
    [Theory]
    [InlineData("2026-10-03", false)]
    [InlineData("2026-10-02", true)]
    public void SusturmaBitisGunuDahilSonrasindaAlarmGeriGelir(string bitis, bool alarmVar)
    {
        var susturmalar = Oku(("Health:Susturulan:0:Kaynak", "Gigi's"), ("Health:Susturulan:0:Bitis", bitis));

        var sonuc = HealthEndpoints.Siniflandir([Kaynak("Gigi's", 36)], Simdi, 26, susturmalar);

        Assert.Equal(alarmVar, sonuc.Bayat.Count == 1);
    }

    // Veritabanındaki ad düz kesme işaretli "Gigi's"; büyük/küçük harf ya da
    // tipografik kesme işareti farkı başka bir kaynak sayılır ve alarm çalar.
    [Fact]
    public void AdBirebirEslesmezseSusturmaz()
    {
        var sonuc = HealthEndpoints.Siniflandir(
            [Kaynak("gigi's", 36), Kaynak("Gigi’s", 36)], Simdi, 26, GigisSusturuldu);

        Assert.Equal(2, sonuc.Bayat.Count);
        Assert.Empty(sonuc.Susturulan);
    }

    [Fact]
    public void TazeKaynakSusturulsaBileHicbirListeyeGirmez()
    {
        var sonuc = HealthEndpoints.Siniflandir([Kaynak("Gigi's", 2)], Simdi, 26, GigisSusturuldu);

        Assert.Empty(sonuc.Bayat);
        Assert.Empty(sonuc.Susturulan);
    }

    // Bozuk kayıt uygulamayı açılışta düşürmüyor, yok sayılıyor: güvenli yön
    // alarmın çalmaya devam etmesi.
    [Fact]
    public void BozukKayitlarYokSayilir()
    {
        var susturmalar = Oku(
            ("Health:Susturulan:0:Kaynak", "Gigi's"), ("Health:Susturulan:0:Bitis", "10.10.2026"),
            ("Health:Susturulan:1:Kaynak", "Bahs"),
            ("Health:Susturulan:2:Bitis", "2026-10-10"),
            ("Health:Susturulan:3:Kaynak", " HIQ "), ("Health:Susturulan:3:Bitis", "2026-10-10"));

        Assert.Equal([new HealthEndpoints.KaynakSusturma("HIQ", new DateOnly(2026, 10, 10))], susturmalar);
    }

    // Bir aydır taranmayan kaynak arıza değil emekli: 503 üretmiyor, ayrı listede.
    [Fact]
    public void EmekliKaynak503Uretmez()
    {
        var sonuc = HealthEndpoints.Siniflandir([Kaynak("Eski", 24 * 31), Kaynak("Taze", 1)], Simdi, 26, []);

        Assert.Empty(sonuc.Bayat);
        Assert.Equal(["Eski"], sonuc.Emekli);
    }

    // Üretim ayarındaki her kayıt geçerli olmalı. Bozuk kayıt sessizce yok
    // sayıldığı için, yazım hatası "susturdum" sanılan alarmı açık bırakırdı.
    [Fact]
    public void UretimAyarindakiSusturmalarGecerli()
    {
        var ayarlar = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json")
            .Build();

        var kayitSayisi = ayarlar.GetSection("Health:Susturulan").GetChildren().Count();

        Assert.Equal(kayitSayisi, HealthEndpoints.SusturmalariOku(ayarlar).Count);
    }
}

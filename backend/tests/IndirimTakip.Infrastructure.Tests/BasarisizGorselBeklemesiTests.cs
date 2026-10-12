using IndirimTakip.Infrastructure.Images;

namespace IndirimTakip.Infrastructure.Tests;

public class BasarisizGorselBeklemesiTests
{
    private static readonly DateTimeOffset Baslangic = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Gun = TimeSpan.FromHours(24);

    [Fact]
    public void Basarisiz_urun_sure_dolana_kadar_bekliyor_sonra_birakiliyor()
    {
        var bekleme = new BasarisizGorselBeklemesi();
        bekleme.BasarisizOldu(5694, Baslangic);

        Assert.Contains(5694, bekleme.Bekleyenler(Baslangic.AddMinutes(10), Gun));
        Assert.Contains(5694, bekleme.Bekleyenler(Baslangic.AddHours(23).AddMinutes(59), Gun));
        Assert.DoesNotContain(5694, bekleme.Bekleyenler(Baslangic.AddHours(24), Gun));
    }

    [Fact]
    public void Yeniden_basarisiz_olan_urunun_suresi_bastan_basliyor_digerleri_etkilenmiyor()
    {
        var bekleme = new BasarisizGorselBeklemesi();
        bekleme.BasarisizOldu(1, Baslangic);
        bekleme.BasarisizOldu(2, Baslangic);

        // Süre doldu, ürün 1 yeniden denendi ve yine indirilemedi.
        Assert.Empty(bekleme.Bekleyenler(Baslangic.AddHours(25), Gun));
        bekleme.BasarisizOldu(1, Baslangic.AddHours(25));

        var bekleyenler = bekleme.Bekleyenler(Baslangic.AddHours(30), Gun);
        Assert.Equal([1], bekleyenler.Order());
    }
}

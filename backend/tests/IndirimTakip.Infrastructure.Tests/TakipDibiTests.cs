using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Infrastructure.Tests;

// "… beri en düşük" rozetinin kuralı. Rozet bir iddia ("takip başladığından beri en düşük"); testlerin çoğu iddianın
// yanlış olacağı durumda rozetin ÇIKMADIĞINI sınıyor.
public class TakipDibiTests
{
    private static readonly DateTimeOffset Simdi = new(2026, 10, 7, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset KirkGunOnce = Simdi.AddDays(-40);

    [Fact]
    public void Otuz_gunun_dibindeki_ve_butun_gecmisin_en_dusugundeki_urun_takip_baslangicini_aliyor() =>
        Assert.Equal(KirkGunOnce, TakipDibi.Baslangic(true, 900m, KirkGunOnce, 900m, Simdi));

    [Fact]
    public void Eskiden_daha_ucuza_satilmis_urun_rozet_almiyor() =>
        Assert.Null(TakipDibi.Baslangic(true, 900m, KirkGunOnce, 850m, Simdi));

    // Otuz günün rozeti yoksa (fiyatı hiç değişmemiş, ya da zamdan sonra olağan fiyatına dönmüş) bu rozet de yok:
    // tüm geçmişin en düşüğüne eşit olmak tek başına "dip" değil.
    [Fact]
    public void Otuz_gunun_rozeti_olmayan_urun_tum_gecmisin_en_dusugunde_olsa_da_rozet_almiyor() =>
        Assert.Null(TakipDibi.Baslangic(false, 900m, KirkGunOnce, 900m, Simdi));

    [Fact]
    public void Otuz_gunden_kisa_gecmiste_rozet_yok_tam_otuz_gunde_var()
    {
        Assert.Null(TakipDibi.Baslangic(true, 900m, Simdi.AddDays(-29), 900m, Simdi));
        Assert.NotNull(TakipDibi.Baslangic(true, 900m, Simdi.AddDays(-TakipDibi.EnAzGecmisGun), 900m, Simdi));
    }

    // Özet henüz hesaplanmamışsa (migration'dan sonraki ilk tarama öncesi) alanlar boş: rozet yok, hata yok.
    [Fact]
    public void Ozet_alanlari_bossa_rozet_yok()
    {
        Assert.Null(TakipDibi.Baslangic(true, 900m, null, 900m, Simdi));
        Assert.Null(TakipDibi.Baslangic(true, 900m, KirkGunOnce, null, Simdi));
    }

    // Ürün sayfasında güncel fiyat canlı, özet bir tarama geride olabilir: yeni bir dip özetten önce görünüyor.
    [Fact]
    public void Ozetteki_en_dusukten_de_dusuk_canli_fiyat_rozet_aliyor() =>
        Assert.Equal(KirkGunOnce, TakipDibi.Baslangic(true, 880m, KirkGunOnce, 900m, Simdi));
}

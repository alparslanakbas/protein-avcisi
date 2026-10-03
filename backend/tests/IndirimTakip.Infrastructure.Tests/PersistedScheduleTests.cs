namespace IndirimTakip.Infrastructure.Tests;

// Deploy konteyneri yeniden başlatıyor. Tarama eskiden her açılışta çalışıyordu,
// yani her deploy bütün kaynakların tam taramasını başlatıyordu; artık açılış
// işi yalnızca son TAMAMLANAN turdan bu yana aralık dolduysa çalıştırıyor.
public class PersistedScheduleTests
{
    private static readonly TimeSpan AltiSaat = TimeSpan.FromHours(6);
    private static readonly DateTimeOffset Simdi = new(2026, 9, 19, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Hic_calismamis_is_hemen_calisir() =>
        Assert.Equal(TimeSpan.Zero, PersistedSchedule.TimeUntilDue(null, AltiSaat, Simdi));

    // DEPLOY DURUMU: son tur bir saat önce bitti, yeniden açılış bütün kaynakları
    // yeniden taramak yerine beş saat bekliyor.
    [Fact]
    public void Tamamlanan_turdan_kisa_sure_sonra_acilis_araligi_bekler() =>
        Assert.Equal(TimeSpan.FromHours(5), PersistedSchedule.TimeUntilDue(Simdi.AddHours(-1), AltiSaat, Simdi));

    [Fact]
    public void Gecikmis_is_hemen_calisir() =>
        Assert.Equal(TimeSpan.Zero, PersistedSchedule.TimeUntilDue(Simdi.AddHours(-9), AltiSaat, Simdi));

    [Fact]
    public void Tam_bir_aralik_sonra_sira_gelmistir() =>
        Assert.Equal(TimeSpan.Zero, PersistedSchedule.TimeUntilDue(Simdi - AltiSaat, AltiSaat, Simdi));

    // GÜNLÜK İŞ (21:00 UTC). Simdi 16:00 UTC; dünün turu 21:50'de bitmişti.
    private const int GunlukSaat = 21;
    private static readonly DateTimeOffset DunkuTur = new(2026, 9, 18, 21, 50, 0, TimeSpan.Zero);

    [Fact]
    public void Gunluk_is_saatinden_once_acilista_bugunku_saati_bekler() =>
        Assert.Equal(TimeSpan.FromHours(5), PersistedSchedule.TimeUntilDailyDue(DunkuTur, GunlukSaat, Simdi));

    // DEPLOY DURUMU: bugünün turu 21:00'de başladı, 21:30'daki deploy kesti ve damga
    // dünkü kaldı. Açılış ertesi gece yarısını beklemeden hemen çalıştırıyor.
    [Fact]
    public void Gunluk_is_kesilen_turu_hemen_yeniden_calistirir() =>
        Assert.Equal(TimeSpan.Zero, PersistedSchedule.TimeUntilDailyDue(DunkuTur, GunlukSaat, Simdi.AddHours(5.5)));

    [Fact]
    public void Gunluk_is_kacirilan_gunu_telafi_eder() =>
        Assert.Equal(TimeSpan.Zero, PersistedSchedule.TimeUntilDailyDue(DunkuTur.AddDays(-1), GunlukSaat, Simdi));

    // Bugünün turu bitti (21:55); 22:10'daki deploy onu tekrar başlatmıyor.
    [Fact]
    public void Gunluk_is_tamamlandiktan_sonra_ertesi_gunu_bekler() =>
        Assert.Equal(TimeSpan.FromHours(22) + TimeSpan.FromMinutes(50),
            PersistedSchedule.TimeUntilDailyDue(Simdi.AddHours(5).AddMinutes(55), GunlukSaat, Simdi.AddHours(6).AddMinutes(10)));

    // Kaydı olmayan iş (bu takvimle ilk açılış) gece taramasını gün ortasında başlatmıyor.
    [Fact]
    public void Gunluk_is_kaydi_yoksa_siradaki_saati_bekler() =>
        Assert.Equal(TimeSpan.FromHours(5), PersistedSchedule.TimeUntilDailyDue(null, GunlukSaat, Simdi));
}

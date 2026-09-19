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
}

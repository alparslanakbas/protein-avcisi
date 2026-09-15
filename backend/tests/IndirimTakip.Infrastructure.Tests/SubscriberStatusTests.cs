using IndirimTakip.Infrastructure.Subscribers;

namespace IndirimTakip.Infrastructure.Tests;

// Paneldeki "aktif", bültenin gönderdiği kümeyle BİREBİR aynı olmalı
// (IsConfirmed && UnsubscribedAt == null); yoksa panel hiç e-posta almayan
// birini aktif gösterir ya da tersi.
public class SubscriberStatusTests
{
    [Fact]
    public void OnayliVeAyrilmamisAktif() =>
        Assert.Equal(SubscriberStatus.Active, SubscriberService.StatusOf(true, null));

    [Fact]
    public void OnaylanmamisKayitBekliyor() =>
        Assert.Equal(SubscriberStatus.Pending, SubscriberService.StatusOf(false, null));

    // Listeden çıkmak IsConfirmed'ı da sıfırlıyor, ama yalnızca tarihi dolu
    // bir satır (eski veri ya da yarım yazma) yine de ASLA aktif sayılmamalı.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AyrilmaTarihiOnaydanBaskin(bool isConfirmed) =>
        Assert.Equal(SubscriberStatus.Unsubscribed, SubscriberService.StatusOf(isConfirmed, DateTimeOffset.UtcNow));
}

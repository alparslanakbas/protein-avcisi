using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndirimTakip.Infrastructure.Tests;

// Bu testler DNS'e çıkıyor: alan adının gerçekten çözümlenip
// çözümlenmediğini doğrulamanın başka yolu yok.
public class EmailAddressValidatorTests
{
    private static EmailAddressValidator Create() =>
        new(NullLogger<EmailAddressValidator>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("duz-metin")]
    [InlineData("bosluklu adres@gmail.com")]
    [InlineData("kullanici@alanadisiz")]
    public async Task BicimiBozukAdresReddediliyor(string email)
        => Assert.False(await Create().IsDeliverableAsync(email));

    [Fact]
    public async Task AtilabilirSaglayiciReddediliyor()
        => Assert.False(await Create().IsDeliverableAsync("birisi@mailinator.com"));

    [Fact]
    public async Task VarOlmayanAlanAdiReddediliyor()
        => Assert.False(await Create().IsDeliverableAsync(
            "kullanici@buboyle-bir-alan-adi-kesinlikle-yok-12873.com"));

    [Theory]
    [InlineData("kullanici@gmail.com")]
    [InlineData("kullanici@proteinavcisi.com.tr")]
    public async Task GercekAlanAdiKabulEdiliyor(string email)
        => Assert.True(await Create().IsDeliverableAsync(email));
}

using IndirimTakip.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// SSRF koruması (DisAgBaglantisi) her istemciye VARSAYILAN olarak takılıyor;
/// ama bir istemci kendi işleyicisini seçerse (ConfigurePrimaryHttpMessageHandler)
/// varsayılanın yerine geçer ve korumayı kendisi kurmazsa sessizce kaybeder.
/// Bu test kayıtlı her istemcinin gerçek işleyici zincirini kurup en içteki
/// işleyicinin bağlantıyı DisAgBaglantisi üzerinden açtığını doğruluyor
/// (güvenlik incelemesi, 27 Eylül). Önbelleği ısıtan istemci Api'de ve bilerek
/// dışarıda: localhost'a gidiyor, burada kayıtlı değil.
/// </summary>
public class HttpIstemcileriDisAgTests
{
    private static (List<string> Korumasiz, int IstemciSayisi) Tara(IServiceCollection services)
    {
        using var sp = services.BuildServiceProvider();
        var fabrika = sp.GetRequiredService<IHttpMessageHandlerFactory>();

        // İşleyiciyi değiştirebilen her şey adlı bir istemci yapılandırması;
        // yapılandırması olmayan istemci zaten varsayılanı (korumalı olanı) alır.
        var adlar = sp.GetServices<IConfigureOptions<HttpClientFactoryOptions>>()
            .OfType<ConfigureNamedOptions<HttpClientFactoryOptions>>()
            .Select(o => o.Name)
            .Where(ad => !string.IsNullOrEmpty(ad))
            .Distinct()
            .ToList();

        var korumasiz = new List<string>();
        foreach (var ad in adlar)
        {
            var isleyici = fabrika.CreateHandler(ad!);
            while (isleyici is DelegatingHandler sarici)
                isleyici = sarici.InnerHandler!;
            // "ConnectCallback dolu" yetmez: başka bir geri çağrı da dolu olur.
            if (isleyici is not SocketsHttpHandler { ConnectCallback: { } geriCagri }
                || geriCagri.Method.DeclaringType != typeof(DisAgBaglantisi))
                korumasiz.Add($"{ad} ({isleyici.GetType().Name})");
        }
        return (korumasiz, adlar.Count);
    }

    private static ServiceCollection AltyapiKayitlari()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        return services;
    }

    [Fact]
    public void Kayitli_her_istemci_dis_ag_korumasiyla_baglaniyor()
    {
        var (korumasiz, sayi) = Tara(AltyapiKayitlari());

        // Sayım yöntemi bozulursa test "hiç istemci yok, hepsi temiz" diye geçmesin.
        Assert.True(sayi >= 10, $"yalnızca {sayi} istemci bulundu; sayım yöntemi bozulmuş olabilir");
        Assert.Empty(korumasiz);
    }

    [Fact]
    public void Kendi_islemcisini_korumasiz_kuran_istemci_yakalaniyor()
    {
        // Negatif kontrol, kalıcı: yarın biri "new SocketsHttpHandler()" yazarsa
        // yukarıdaki test tam olarak bunu görmeli.
        var services = AltyapiKayitlari();
        services.AddHttpClient("korumasiz-deneme")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler());

        var (korumasiz, _) = Tara(services);

        Assert.Equal(["korumasiz-deneme (SocketsHttpHandler)"], korumasiz);
    }
}

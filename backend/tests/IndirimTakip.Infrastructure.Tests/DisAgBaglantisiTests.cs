using System.Net;
using System.Net.Sockets;
using System.Text;
using IndirimTakip.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// SSRF koruması: kaynaklardan gelen adreslere istek atan istemciler iç ağa
/// bağlanmamalı (güvenlik incelemesi, 26 Eylül).
/// </summary>
public class DisAgBaglantisiTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.17.0.2")]        // Docker köprü ağı
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // bulut makine bilgisi ucu
    [InlineData("100.64.0.1")]        // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")]  // IPv6 kılığında yerel adres
    [InlineData("::ffff:169.254.169.254")]
    public void Ic_adresler_reddediliyor(string adres)
    {
        Assert.False(DisAgBaglantisi.GenelAdresMi(IPAddress.Parse(adres)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("104.16.0.1")]
    [InlineData("172.32.0.1")]        // 172.16/12'nin hemen dışı
    [InlineData("100.128.0.1")]       // CGNAT'ın hemen dışı
    [InlineData("2606:4700::1111")]
    public void Genel_adresler_kabul_ediliyor(string adres)
    {
        Assert.True(DisAgBaglantisi.GenelAdresMi(IPAddress.Parse(adres)));
    }

    /// <summary>
    /// Asıl kanıt: yerelde gerçekten dinleyen bir sunucu var. Düz işleyici
    /// ona ulaşıyor (testin ölçtüğü şeyin gerçek olduğunu gösteren kontrol),
    /// korumalı işleyici bağlantı bile kurmuyor.
    /// </summary>
    [Fact]
    public async Task Korumali_istemci_yerel_sunucuya_baglanmiyor()
    {
        using var dinleyici = new TcpListener(IPAddress.Loopback, 0);
        dinleyici.Start();
        var port = ((IPEndPoint)dinleyici.LocalEndpoint).Port;
        var kabulEdilen = 0;
        var sunucu = Task.Run(async () =>
        {
            while (true)
            {
                using var istemci = await dinleyici.AcceptTcpClientAsync();
                Interlocked.Increment(ref kabulEdilen);
                var akis = istemci.GetStream();
                await akis.ReadAsync(new byte[4096]);
                await akis.WriteAsync(Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"));
            }
        });

        using (var duz = new HttpClient(new SocketsHttpHandler()))
        {
            Assert.Equal("ok", await duz.GetStringAsync($"http://127.0.0.1:{port}/"));
        }
        Assert.Equal(1, kabulEdilen);

        using var korumali = new HttpClient(DisAgBaglantisi.IsleyiciOlustur());
        await Assert.ThrowsAsync<HttpRequestException>(
            () => korumali.GetStringAsync($"http://127.0.0.1:{port}/"));
        // Ad üzerinden de: localhost DNS ile yerel adrese çözülüyor.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => korumali.GetStringAsync($"http://localhost:{port}/"));

        Assert.Equal(1, kabulEdilen);
        dinleyici.Stop();
    }

    /// <summary>
    /// Koruma varsayılan olarak her istemciye geliyor; iç adrese bilerek giden
    /// istemci (önbellek ısıtma) kendi işleyicisini seçince o kazanmalı.
    /// </summary>
    [Fact]
    public void Varsayilan_her_istemcide_acik_secim_onu_eziyor()
    {
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b =>
            b.ConfigurePrimaryHttpMessageHandler(DisAgBaglantisi.IsleyiciOlustur));
        services.AddHttpClient("scraper");
        services.AddHttpClient("isitma")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler());
        using var sp = services.BuildServiceProvider();
        var fabrika = sp.GetRequiredService<IHttpMessageHandlerFactory>();

        Assert.NotNull(AsilIsleyici(fabrika.CreateHandler("scraper")).ConnectCallback);
        Assert.NotNull(AsilIsleyici(fabrika.CreateHandler(string.Empty)).ConnectCallback);
        Assert.Null(AsilIsleyici(fabrika.CreateHandler("isitma")).ConnectCallback);
    }

    private static SocketsHttpHandler AsilIsleyici(HttpMessageHandler isleyici)
    {
        while (isleyici is DelegatingHandler zincir)
            isleyici = zincir.InnerHandler!;
        return Assert.IsType<SocketsHttpHandler>(isleyici);
    }
}

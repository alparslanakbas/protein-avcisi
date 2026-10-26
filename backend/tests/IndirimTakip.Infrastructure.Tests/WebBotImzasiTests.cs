using System.Text;
using System.Text.RegularExpressions;
using IndirimTakip.Infrastructure.Scraping;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Web Bot Auth imzası. Temel adımlar RFC'lerin kendi test vektörlerine sabitli (WheyProof'un
/// WebBotAuthTests'iyle aynı vektörler) ve üretilen her imza bir Ed25519 doğrulayıcıyla sınanıyor:
/// başlığı olan ama doğrulanmayan bir imza, Shopify forumundaki tarayıcıyı imzasız saydırmıştı.
/// </summary>
public class WebBotImzasiTests
{
    // RFC 9421 Ek B.1.4, test-key-ed25519.
    private const string RfcTohum = "n4Ni-HpISpVObnQMW0wOhCKROaIKqKtW_2ZYb2p9KcU";
    private const string RfcAcikAnahtar = "JrQLj5P_89iXES9-vFgrIy29clF9CC_oPPsw3c5D0bs";
    private const string Ajan = "https://api.wheyproof.com";
    private const string Ua = "Mozilla/5.0 (compatible; wheyproofbot/1.0; +https://www.wheyproof.com/bot)";

    private sealed class SabitSaat(DateTimeOffset an) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => an;
    }

    private static readonly SabitSaat Saat = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    private static byte[] Base64UrlCoz(string s)
    {
        var b = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b + new string('=', (4 - b.Length % 4) % 4));
    }

    private static WebBotImzasi Imza(bool imzala = true) => new(Base64UrlCoz(RfcTohum), Ajan, Ua, Saat, imzala);

    private static bool Dogrulanir(string acikAnahtar, string imzaTabani, string imzaBase64)
    {
        var dogrulayici = new Ed25519Signer();
        dogrulayici.Init(false, new Ed25519PublicKeyParameters(Base64UrlCoz(acikAnahtar)));
        var veri = Encoding.ASCII.GetBytes(imzaTabani);
        dogrulayici.BlockUpdate(veri, 0, veri.Length);
        return dogrulayici.VerifySignature(Convert.FromBase64String(imzaBase64));
    }

    [Fact]
    public void Imza_rfc_9421_ed25519_ornegiyle_ayni()
    {
        // RFC 9421 Ek B.2.6, satır kırmaları kaldırılmış.
        var taban = WebBotImzasi.ImzaTabani(
            [
                ("date", "Tue, 20 Apr 2021 02:07:55 GMT"), ("@method", "POST"), ("@path", "/foo"),
                ("@authority", "example.com"), ("content-type", "application/json"), ("content-length", "18"),
            ],
            "(\"date\" \"@method\" \"@path\" \"@authority\" \"content-type\" \"content-length\");created=1618884473;keyid=\"test-key-ed25519\"");

        var imza = WebBotImzasi.TabaniImzala(new Ed25519PrivateKeyParameters(Base64UrlCoz(RfcTohum)), taban);

        Assert.Equal("wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4A0w6vuQv5lIp5WPpBKRCw==", imza);
    }

    [Fact]
    public void Keyid_rfc_8037_jwk_parmak_izi()
    {
        // RFC 8037 Ek A.3.
        Assert.Equal("kPrK_qmxVWaYVA9wwBF6Iuo3vVzz7TxHCTwXBygrS4k", WebBotImzasi.ParmakIzi("11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHURo"));
    }

    [Fact]
    public void Acik_anahtar_ve_keyid_tohumdan_geliyor()
    {
        var imza = Imza();

        Assert.True(imza.AnahtarVar);
        Assert.Equal(RfcAcikAnahtar, imza.AcikAnahtar);
        Assert.Equal(WebBotImzasi.ParmakIzi(RfcAcikAnahtar), imza.AnahtarKimligi);
    }

    [Fact]
    public void Imzali_istek_uc_basligi_tasiyor_ve_imza_dogrulaniyor()
    {
        var imza = Imza();
        var istek = new HttpRequestMessage(HttpMethod.Get, "https://www.CommanderNutrition.com/products.json?limit=250&page=1");
        istek.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0");

        imza.Uygula(istek);

        var ajan = Assert.Single(istek.Headers.GetValues("Signature-Agent"));
        var girdi = Assert.Single(istek.Headers.GetValues("Signature-Input"));
        var deger = Assert.Single(istek.Headers.GetValues("Signature"));

        // Tırnaklı bir https adresi, sözlük üyesi değil (Cloudflare rehberi: sig1="..." geçmiyor).
        Assert.Equal("\"https://api.wheyproof.com\"", ajan);
        Assert.StartsWith("sig1=(\"@authority\" \"signature-agent\");", girdi);
        Assert.Contains($";keyid=\"{imza.AnahtarKimligi}\"", girdi);
        Assert.Contains(";alg=\"ed25519\"", girdi);
        Assert.Contains(";tag=\"web-bot-auth\"", girdi);
        var olusturma = long.Parse(Regex.Match(girdi, @";created=(\d+)").Groups[1].Value);
        var bitis = long.Parse(Regex.Match(girdi, @";expires=(\d+)").Groups[1].Value);
        Assert.Equal(Saat.GetUtcNow().ToUnixTimeSeconds(), olusturma);
        Assert.Equal(60, bitis - olusturma);

        var parametreler = girdi["sig1=".Length..];
        var taban = $"\"@authority\": www.commandernutrition.com\n\"signature-agent\": {ajan}\n\"@signature-params\": {parametreler}";
        Assert.True(Dogrulanir(imza.AcikAnahtar!, taban, Regex.Match(deger, "^sig1=:(.+):$").Groups[1].Value));

        Assert.Equal(Ua, istek.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData("https://www.bahsbar.com/products.json", "www.bahsbar.com")]
    [InlineData("https://WWW.BahsBar.COM/p", "www.bahsbar.com")]
    [InlineData("https://magaza.example:8443/p", "magaza.example:8443")]
    public void Yetki_kucuk_harfli_host_varsayilan_olmayan_portla(string adres, string beklenen) =>
        Assert.Equal(beklenen, WebBotImzasi.Yetki(new Uri(adres)));

    // Canlıdaki durum (Shopify kaydı onaylanana kadar): istekler eskisi gibi çıkıyor.
    [Fact]
    public void Imzalama_kapaliyken_istek_degismiyor()
    {
        var imza = Imza(imzala: false);
        var istek = new HttpRequestMessage(HttpMethod.Get, "https://www.bahsbar.com/products.json");
        istek.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0");

        imza.Uygula(istek);

        Assert.False(istek.Headers.Contains("Signature"));
        Assert.False(istek.Headers.Contains("Signature-Agent"));
        Assert.Contains("Chrome/128.0", istek.Headers.UserAgent.ToString());
        Assert.True(imza.AnahtarVar);
    }

    [Fact]
    public void Anahtar_yoksa_sorunsuz_kapali()
    {
        var imza = WebBotImzasi.Olustur(null, Ajan, Ua, Saat);

        Assert.False(imza.AnahtarVar);
        Assert.Null(imza.Sorun);
    }

    [Theory]
    [InlineData("base64 değil!", Ajan, Ua)]
    [InlineData("AAAA", Ajan, Ua)]                              // 3 bayt
    [InlineData(null, "http://api.wheyproof.com", Ua)]           // tohum aşağıda dolduruluyor
    [InlineData(null, "https://api.wheyproof.com/keys", Ua)]
    [InlineData(null, Ajan, "")]
    public void Kullanilamayan_ayar_imzayi_gerekcesiyle_kapatiyor(string? tohum, string ajan, string ua)
    {
        var imza = WebBotImzasi.Olustur(tohum ?? Convert.ToBase64String(Base64UrlCoz(RfcTohum)), ajan, ua, Saat);

        Assert.False(imza.AnahtarVar);
        Assert.NotNull(imza.Sorun);
    }

    [Fact]
    public async Task Isleyici_yalnizca_listedeki_adresleri_imzaliyor()
    {
        var ic = new KaydedenIsleyici();
        var istemci = new HttpMessageInvoker(new WebBotImzaIsleyicisi(Imza(), RobotsTxtIsleyicisi.ShopifyAdresleri) { InnerHandler = ic });

        await istemci.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://www.bahsbar.com/products/x"), CancellationToken.None);
        await istemci.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://www.supplementler.com/p/x"), CancellationToken.None);

        Assert.True(ic.Istekler[0].Headers.Contains("Signature"));
        Assert.False(ic.Istekler[1].Headers.Contains("Signature"));
    }

    private sealed class KaydedenIsleyici : HttpMessageHandler
    {
        public List<HttpRequestMessage> Istekler { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Add(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

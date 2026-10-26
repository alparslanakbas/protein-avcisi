using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Shopify isteklerini Web Bot Auth ile imzalar (HTTP Message Signatures, RFC 9421).
/// </summary>
/// <remarks>
/// <b>NEDEN (26 Ekim).</b> Shopify imzasız botları en sıkı sınıra tutuyor, kayıtlı ve imzalı botlara
/// daha yüksek sınır veriyor (changelog, 30 Mayıs 2026'dan beri). Kayıt WheyProof adına yapıldı
/// (bot <c>wheyproofbot</c>, form 8 ve 18 Ekim); bu site o başvuruda "kardeş site" diye geçiyor ve
/// robots.txt'de de aynı adla okuyor (bkz. <see cref="RobotsTxtIsleyicisi.UrunAdi"/>). Yani aynı
/// anahtar, aynı Signature-Agent ve aynı User-Agent: Shopify'a tek bot görünüyoruz.
///
/// <b>WheyProof'un <c>WebBotAuth</c> sınıfının imzalama kısmı.</b> Biçim ayrıntıları orada ölçülerek
/// sabitlendi (Signature-Agent TIRNAKLI bir https adresi; keyid JWK parmak izi; yalnız Ed25519);
/// burada değiştirilirse orada da değiştirilmeli, test vektörleri iki depoda aynı. Anahtar dizinini
/// WheyProof yayınlıyor (<c>https://api.wheyproof.com/.well-known/http-message-signatures-directory</c>);
/// bu yüzden anahtar iki sitenin .env'inde AYNI (26 Ekim'de özetle karşılaştırıldı).
///
/// <b>İmzalama şu an KAPALI</b> (<c>WebBotAuth:SignRequests: false</c>): 8 Ekim'de kayıtsız imzalı
/// istekler sunucudan da evden de 429 aldı, imzasızlar evden 200. Shopify kaydı onaylayınca açılacak.
/// Anahtar yoksa ya da bozuksa da kapalı; bozuk ayar siteyi durdurmuyor, açılışta <see cref="Sorun"/>
/// günlüğe yazılıyor.
/// </remarks>
public sealed class WebBotImzasi
{
    internal static readonly TimeSpan IstekGecerliligi = TimeSpan.FromMinutes(1);

    private readonly Ed25519PrivateKeyParameters? anahtar;
    private readonly TimeProvider saat;

    public WebBotImzasi(byte[]? tohum, string? imzaAjani, string? kullaniciAjani, TimeProvider saat, bool imzala = true)
    {
        this.saat = saat;
        Imzala = imzala;
        ImzaAjani = imzaAjani?.Trim() ?? "";
        KullaniciAjani = kullaniciAjani?.Trim() ?? "";

        if (tohum is null)
            return;
        if (tohum.Length != Ed25519PrivateKeyParameters.KeySize)
        {
            Sorun = $"anahtar {tohum.Length} bayt, {Ed25519PrivateKeyParameters.KeySize} olmalı";
            return;
        }
        if (!Uri.TryCreate(ImzaAjani, UriKind.Absolute, out var ajan) || ajan.Scheme != Uri.UriSchemeHttps
            || ajan.AbsolutePath != "/" || ajan.Query.Length > 0)
        {
            Sorun = $"WebBotAuth:SignatureAgent '{ImzaAjani}' bir https kökü değil";
            return;
        }
        if (KullaniciAjani.Length == 0)
        {
            Sorun = "WebBotAuth:UserAgent boş";
            return;
        }

        ImzaAjani = ajan.GetLeftPart(UriPartial.Authority);
        anahtar = new Ed25519PrivateKeyParameters(tohum);
        AcikAnahtar = Base64Url(anahtar.GeneratePublicKey().GetEncoded());
        AnahtarKimligi = ParmakIzi(AcikAnahtar);
    }

    /// <summary>Ayardan: base64 tohum, Signature-Agent kökü, User-Agent.</summary>
    public static WebBotImzasi Olustur(string? tohumBase64, string? imzaAjani, string? kullaniciAjani, TimeProvider saat,
        bool imzala = true)
    {
        if (string.IsNullOrWhiteSpace(tohumBase64))
            return new WebBotImzasi(null, imzaAjani, kullaniciAjani, saat, imzala);

        byte[] tohum;
        try
        {
            tohum = Convert.FromBase64String(tohumBase64.Trim());
        }
        catch (FormatException)
        {
            return new WebBotImzasi([], imzaAjani, kullaniciAjani, saat, imzala);
        }
        return new WebBotImzasi(tohum, imzaAjani, kullaniciAjani, saat, imzala);
    }

    /// <summary>Kullanılabilir bir anahtar ayarlı.</summary>
    public bool AnahtarVar => anahtar is not null;

    /// <summary>İstekler imzalanıyor (ve botun User-Agent'ıyla gidiyor).</summary>
    public bool Imzala { get; }

    /// <summary>Ayarlı bir anahtarın neden kullanılmadığı; açıkken ya da hiç ayarlanmamışken null.</summary>
    public string? Sorun { get; }

    /// <summary>İsteklerin Signature-Agent'ta andığı kök; anahtar dizini orada.</summary>
    public string ImzaAjani { get; }

    /// <summary>İmzalı isteklerin User-Agent'ı (Shopify kayıttakiyle karşılaştırıyor).</summary>
    public string KullaniciAjani { get; }

    /// <summary>Açık anahtar, base64url (JWK "x").</summary>
    public string? AcikAnahtar { get; }

    /// <summary>Açık anahtarın JWK SHA-256 parmak izi (RFC 8037 A.3), yani "keyid".</summary>
    public string? AnahtarKimligi { get; }

    /// <summary>Signature-Agent, Signature-Input ve Signature başlıklarını ekler, botun User-Agent'ını koyar.</summary>
    public void Uygula(HttpRequestMessage istek)
    {
        if (anahtar is null || !Imzala || istek.RequestUri is null)
            return;

        var olusturma = saat.GetUtcNow().ToUnixTimeSeconds();
        var bitis = olusturma + (long)IstekGecerliligi.TotalSeconds;
        var ajan = $"\"{ImzaAjani}\"";
        var parametreler = $"(\"@authority\" \"signature-agent\");created={olusturma};keyid=\"{AnahtarKimligi}\";alg=\"ed25519\""
                           + $";expires={bitis};nonce=\"{Nonce()}\";tag=\"web-bot-auth\"";
        var imza = TabaniImzala(anahtar, ImzaTabani([("@authority", Yetki(istek.RequestUri)), ("signature-agent", ajan)], parametreler));

        var basliklar = istek.Headers;
        basliklar.Remove("Signature-Agent");
        basliklar.Remove("Signature-Input");
        basliklar.Remove("Signature");
        basliklar.TryAddWithoutValidation("Signature-Agent", ajan);
        basliklar.TryAddWithoutValidation("Signature-Input", $"sig1={parametreler}");
        basliklar.TryAddWithoutValidation("Signature", $"sig1=:{imza}:");
        basliklar.UserAgent.Clear();
        basliklar.TryAddWithoutValidation("User-Agent", KullaniciAjani);
    }

    /// <summary>RFC 9421 bölüm 2.5: kapsanan her bileşen bir satır, sonra parametreler; sonda satır sonu yok.</summary>
    internal static string ImzaTabani(IReadOnlyList<(string Kimlik, string Deger)> bilesenler, string parametreler)
    {
        var satirlar = bilesenler.Select(b => $"\"{b.Kimlik}\": {b.Deger}").Append($"\"@signature-params\": {parametreler}");
        return string.Join("\n", satirlar);
    }

    internal static string TabaniImzala(Ed25519PrivateKeyParameters anahtar, string imzaTabani)
    {
        var veri = Encoding.ASCII.GetBytes(imzaTabani);
        var imzalayici = new Ed25519Signer();
        imzalayici.Init(true, anahtar);
        imzalayici.BlockUpdate(veri, 0, veri.Length);
        return Convert.ToBase64String(imzalayici.GenerateSignature());
    }

    /// <summary>RFC 9421 bölüm 2.2.3: küçük harfli host; port yalnız şemanın varsayılanı değilse.</summary>
    internal static string Yetki(Uri adres) =>
        (adres.IsDefaultPort ? adres.IdnHost : $"{adres.IdnHost}:{adres.Port}").ToLowerInvariant();

    /// <summary>RFC 7638, zorunlu üyeler sözlük sırasıyla (OKP anahtarı için RFC 8037 A.3).</summary>
    internal static string ParmakIzi(string x) =>
        Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes($"{{\"crv\":\"Ed25519\",\"kty\":\"OKP\",\"x\":\"{x}\"}}")));

    internal static string Base64Url(byte[] baytlar) =>
        Convert.ToBase64String(baytlar).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Nonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

/// <summary>
/// Giden istekleri <see cref="WebBotImzasi"/> ile imzalar; istenirse yalnız belli adreslerde.
/// </summary>
/// <remarks>
/// Shopify çekicilerinde robots.txt işleyicisinden SONRA (izin verilmeyen istek imzalanmıyor) ve
/// tünel işleyicisinden ÖNCE takılı: tünelden tekrarlanan istek aynı imzayı taşıyor (imza hostu
/// kapsıyor, o değişmiyor). Puan tazelemesi her mağazaya gidiyor; orada yalnız Shopify adresleri
/// imzalanıyor, diğer mağazalar bazılarının istediği tarayıcı User-Agent'ıyla kalıyor.
/// </remarks>
public sealed class WebBotImzaIsleyicisi(WebBotImzasi imza, IReadOnlySet<string>? yalnizcaAdresler = null) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (imza.AnahtarVar && request.RequestUri is { } adres && (yalnizcaAdresler is null || yalnizcaAdresler.Contains(adres.Host)))
            imza.Uygula(request);
        return base.SendAsync(request, cancellationToken);
    }
}

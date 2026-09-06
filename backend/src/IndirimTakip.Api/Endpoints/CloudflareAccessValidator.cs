using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IndirimTakip.Api.Endpoints;

/// <summary>
/// Cloudflare Access'in isteklere eklediği imzalı kimlik jetonunu doğrular.
/// </summary>
/// <remarks>
/// <b>NEDEN VAR.</b> Panel iki kapının arkasında: Access (kenarda, e-posta
/// doğrulaması) ve admin anahtarı (uygulamada). İkinci kapı gerçek bir katman
/// ama zahmetliydi — 44 karakterlik anahtar yalnızca sunucudaki .env
/// dosyasında duruyor ve oturum her 12 saatte bir yeniden soruluyordu.
/// Access zaten kimliği doğruluyor ve bunu KRİPTOGRAFİK OLARAK imzalı bir
/// jetonla bildiriyor; o jetonu doğrulamak, elle girilen bir parolayı kabul
/// etmekten hem daha kolay hem daha sağlam. Anahtar yolu KALDIRILMADI —
/// betikler ve `api.` alt alan adı onu kullanmaya devam ediyor.
///
/// <b>YAPILANDIRILMAMIŞSA KAPALI (fail-closed).</b> Ekip alanı ya da izleyici
/// (audience) etiketi tanımlı değilse bu yol tamamen devre dışı kalıyor.
/// Alternatifi — eksik ayarda jetonu yine de kabul etmek — yanlış bir
/// yapılandırmanın kapıyı sessizce açması demekti.
///
/// <b>AUDIENCE KONTROLÜ ZORUNLU.</b> Yalnızca imza ve ihraççıyı doğrulamak
/// yetmez: aynı Cloudflare hesabındaki BAŞKA bir uygulama için üretilmiş
/// geçerli bir jeton da imzayı geçerdi. `aud` etiketi jetonu BU uygulamaya
/// bağlıyor.
/// </remarks>
public sealed class CloudflareAccessValidator
{
    public const string HeaderName = "Cf-Access-Jwt-Assertion";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<CloudflareAccessValidator> logger;
    private readonly string? issuer;
    private readonly string? audience;
    private readonly string? certsUrl;

    // JWKS her istekte indirilmez; Cloudflare anahtarları nadiren döndürüyor.
    // Bilinmeyen bir kid görülürse önbellek süresi beklenmeden tazeleniyor,
    // yoksa anahtar döndüğü an panel erişilemez olurdu.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim yenilemeKilidi = new(1, 1);
    private IReadOnlyCollection<JsonWebKey> keys = [];
    private DateTimeOffset keysFetchedAt = DateTimeOffset.MinValue;

    public CloudflareAccessValidator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<CloudflareAccessValidator> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;

        var teamDomain = configuration["CloudflareAccess:TeamDomain"]?.Trim();
        audience = configuration["CloudflareAccess:Aud"]?.Trim();

        if (!string.IsNullOrEmpty(teamDomain) && !string.IsNullOrEmpty(audience))
        {
            issuer = "https://" + teamDomain;
            certsUrl = issuer + "/cdn-cgi/access/certs";
        }
    }

    /// <summary>Doğrulama yapılandırılmış mı.</summary>
    public bool Enabled => certsUrl is not null;

    /// <summary>
    /// İstekteki Access jetonu geçerliyse true.
    /// </summary>
    public async Task<bool> GecerliMi(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return false;

        var token = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var sonuc = await DogrulaAsync(token, tazele: false, cancellationToken);

            // İmza tutmadıysa anahtar dönmüş olabilir: bir kez tazeleyip yeniden dene.
            if (!sonuc && await AnahtarlariTazeleAsync(cancellationToken))
                sonuc = await DogrulaAsync(token, tazele: true, cancellationToken);

            return sonuc;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cloudflare Access jetonu doğrulanamadı.");
            return false;
        }
    }

    private async Task<bool> DogrulaAsync(string token, bool tazele, CancellationToken cancellationToken)
    {
        if (!tazele && (keys.Count == 0 || DateTimeOffset.UtcNow - keysFetchedAt > CacheLifetime))
        {
            await AnahtarlariTazeleAsync(cancellationToken);
        }

        if (keys.Count == 0)
            return false;

        var handler = new JsonWebTokenHandler();
        var sonuc = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidateIssuer = true,
            // Jetonu BU uygulamaya bağlayan kontrol; bkz. sınıf açıklaması.
            ValidAudience = audience,
            ValidateAudience = true,
            IssuerSigningKeys = keys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            // Cloudflare RS256 kullanıyor. Listeyi sabitlemek "alg" karışıklığı
            // saldırılarını (ör. jetonun kendi başlığında zayıf bir algoritma
            // bildirmesi) baştan kapatıyor.
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(2),
        });

        return sonuc.IsValid;
    }

    private async Task<bool> AnahtarlariTazeleAsync(CancellationToken cancellationToken)
    {
        if (certsUrl is null)
            return false;

        await yenilemeKilidi.WaitAsync(cancellationToken);
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var json = await client.GetStringAsync(certsUrl, cancellationToken);
            var jwks = new JsonWebKeySet(json);
            if (jwks.Keys.Count == 0)
                return false;

            keys = jwks.Keys.ToList();
            keysFetchedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            // Anahtar indirilemezse ELDEKİ anahtarlar korunuyor: geçici bir ağ
            // hatası yüzünden paneli kilitlemek gereksiz.
            logger.LogWarning(ex, "Cloudflare Access anahtarları indirilemedi.");
            return false;
        }
        finally
        {
            yenilemeKilidi.Release();
        }
    }
}

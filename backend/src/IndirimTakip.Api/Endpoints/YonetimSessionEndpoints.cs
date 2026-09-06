using System.Security.Cryptography;
using System.Text;
using IndirimTakip.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace IndirimTakip.Api.Endpoints;

/// <summary>
/// Yönetim panelinin oturum açma/kapama uçları.
/// </summary>
/// <remarks>
/// <b>ANAHTAR TARAYICIDA SAKLANMIYOR.</b> Panel admin anahtarını bir kez
/// alıyor, sunucu doğrulayıp HttpOnly çerez veriyor ve anahtar bir daha
/// hiçbir yere yazılmıyor. Anahtarı localStorage'da tutmak en kolay yol
/// olurdu ama o anahtar BÜTÜN ABONELERE e-posta gönderebiliyor (5 Eylül
/// tehdit modelindeki en yüksek hasarlı madde); JavaScript'in okuyabildiği
/// bir yerde durması, tek bir XSS'in bülteni ele geçirmesi demek olurdu.
/// HttpOnly çerezi JavaScript okuyamaz.
///
/// <b>Oturum sunucuda saklanmıyor, imzalı jeton kullanılıyor.</b> Data
/// Protection ile süreli koruma: jeton kendi son kullanma tarihini taşıyor,
/// tablo/temizlik gerekmiyor. Yan etkisi bilinçli — anahtarlar konteynerde
/// durduğu için her deploy sonrası yeniden giriş gerekiyor. Bu bir kusur
/// değil, ucuz bir güvenlik özelliği: açık kalmış bir oturum deploy'da
/// kendiliğinden kapanıyor.
/// </remarks>
internal static class YonetimSessionEndpoints
{
    public const string CookieName = "pa_yonetim";
    private const string ProtectorPurpose = "proteinavcisi.yonetim.oturum";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    public static void MapYonetimSession(this WebApplication app, string? adminApiKey)
    {
        // [FromServices] ZORUNLU, kaldirilmamali. Minimal API gövdeden gelen
        // parametre varken IDataProtectionProvider'i servis olarak ÇIKARAMIYOR
        // ve "Failure to infer one or more parameters" ile patlıyor. Hata
        // derlemede değil, uçlar sayılırken ortaya çıkıyor — ve uç listesinin
        // tamamını düşürdüğü için API'nin HER ucu 500 veriyor.
        // 6 Eylül'de canlıda tam bu yaşandı.
        app.MapPost("/api/dev/session", (
            GirisIstegi istek,
            [FromServices] IDataProtectionProvider dataProtection,
            HttpContext context) =>
        {
            if (string.IsNullOrEmpty(adminApiKey) || !SabitZamanliEsit(istek.Key, adminApiKey))
            {
                // 401 dönüyoruz ve BU KAYDA GİRİYOR: SecurityEventMiddleware
                // 401'leri zaten yazıyor, yani panele girmeye çalışan herkes
                // adresiyle birlikte panelin kendi olay akışında görünecek.
                return Results.Unauthorized();
            }

            var jeton = Koruyucu(dataProtection).Protect("yonetici", SessionLifetime);

            context.Response.Cookies.Append(CookieName, jeton, new CookieOptions
            {
                HttpOnly = true,   // JavaScript okuyamaz
                Secure = true,     // yalnızca HTTPS
                SameSite = SameSiteMode.Strict,
                // Çerez yalnızca panel yolunda gönderiliyor; sitenin geri kalanı
                // bu çerezi hiç görmüyor.
                Path = "/yonetim",
                MaxAge = SessionLifetime,
                IsEssential = true,
            });

            return Results.Ok(new { ok = true });
        })
        // Deneme yanılmayı işlevsiz kılan sınır. Uçun kendi adı da tahmin
        // edilebilir olduğu için burada gevşek bir limit anlamsız olurdu.
        .RequireRateLimiting("yonetim-giris");

        app.MapDelete("/api/dev/session", (HttpContext context) =>
        {
            context.Response.Cookies.Delete(CookieName, new CookieOptions
            {
                Path = "/yonetim",
                Secure = true,
                SameSite = SameSiteMode.Strict,
            });
            return Results.Ok(new { ok = true });
        });
    }

    /// <summary>Çerezdeki oturum jetonu geçerliyse true.</summary>
    public static bool GecerliOturum(HttpContext context, IDataProtectionProvider dataProtection)
    {
        var jeton = context.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(jeton))
            return false;

        try
        {
            return Koruyucu(dataProtection).Unprotect(jeton) == "yonetici";
        }
        catch (CryptographicException)
        {
            // Süresi dolmuş, kurcalanmış ya da deploy sonrası anahtarı değişmiş.
            return false;
        }
    }

    private static ITimeLimitedDataProtector Koruyucu(IDataProtectionProvider provider) =>
        provider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();

    // Uzunluk farkı da sızdırmasın diye önce özet alınıyor: FixedTimeEquals
    // farklı uzunluktaki dizilerde erken dönerdi ve bu, anahtarın uzunluğunu
    // ölçmeye izin verirdi.
    private static bool SabitZamanliEsit(string? verilen, string beklenen)
    {
        if (string.IsNullOrEmpty(verilen))
            return false;

        var a = SHA256.HashData(Encoding.UTF8.GetBytes(verilen));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(beklenen));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    internal record GirisIstegi(string? Key);
}

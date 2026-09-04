using IndirimTakip.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Api.Endpoints;

/// <summary>
/// Kaynak tazeliği sağlık ucu — dışarıdan izlenmek için.
///
/// <b>NEDEN VAR.</b> Taramanın SESSİZCE durması bu projedeki en sinsi arıza
/// tipi: site çalışmaya devam ediyor, sayfalar açılıyor, hiçbir yerde hata
/// görünmüyor. Sorun ancak 48 saat sonra, ürünler bayatlama eşiğini geçip
/// listelerden düşünce fark ediliyor — o da ancak biri siteye bakarsa.
///
/// Somut tetikleyici: Supplementler artık WireGuard tüneliyle toplanıyor ve
/// tünel düşerse toplayıcı DOĞRU davranıp turu atlıyor (boş veri göndermiyor),
/// ama bunu yalnızca kimsenin okumadığı bir log dosyasına yazıyor. Aynı sessiz
/// arıza her kaynak için mümkün: site yapısını değiştirir, IP'mizi engeller,
/// scraper'ın regex'i tutmaz olur.
///
/// Bu uç UptimeRobot gibi bir izleyicinin okuyabilmesi için AÇIK ve
/// kimlik doğrulamasız: içeriği zaten herkese açık bilgi (marka/satıcı adları
/// ve son tarama zamanı, sitede de görünüyor). Yazma yapmıyor.
/// </summary>
internal static class HealthEndpoints
{
    /// <summary>
    /// Bir kaynağın "bayat" sayılması için geçmesi gereken süre.
    ///
    /// 26 saat SEÇİLDİ, 6 değil: kaynakların hepsi 6 saatte bir taranmıyor.
    /// protein7 ve Provitamin günde bir kez (00:00 TSİ), Supplementler günde
    /// iki kez (09:30/21:30 TSİ) çalışıyor. En seyrek kaynak günlük olduğu için
    /// eşik 24 saat + 2 saat pay. Bu, listelerin kullandığı 48 saatlik
    /// bayatlama eşiğinin ALTINDA kalıyor — yani ürünler siteden düşmeden
    /// önce haber alıyoruz, düzeltmek için ~22 saat kalıyor.
    ///
    /// Yapılandırmadan (<c>Health:StaleHours</c>) ezilebiliyor. Sebebi sadece
    /// esneklik değil: bu bir ALARM ve hiç çalıştığı görülmemiş bir alarm,
    /// olmayan alarmdan beterdir. Eşiği geçici olarak düşürmek, 503 yolunun
    /// gerçekten çalıştığını CANLIDA kanıtlamanın tek yolu — ve alarm
    /// kurulduktan sonra da eşiği deploy yapmadan ayarlayabilmek gerekiyor.
    /// </summary>
    private const int VarsayilanBayatlikSaati = 26;

    /// <summary>
    /// Bundan eskisi "arıza" değil, "emekli kaynak" sayılıyor.
    ///
    /// Gerekçe: devre dışı bıraktığımız bir kaynağın ürünleri veritabanında
    /// kalmaya devam ediyor (fiyat geçmişi kaybolmasın diye, bilinçli bir
    /// karar). Bu satırlar eşiği sonsuza kadar aşacağı için uç KALICI OLARAK
    /// kırmızı kalırdı — ve sürekli kırmızı yanan bir alarm, bakılmayan bir
    /// alarma dönüşür. Bir aydır güncellenmeyen bir kaynak yeni bir haber
    /// değil; gövdede bilgi olarak listeleniyor ama 503 ÜRETMİYOR.
    /// </summary>
    private const int EmekliGunu = 30;

    public static void MapHealthEndpoints(this WebApplication app)
    {
        var bayatlikSaati = app.Configuration.GetValue<int?>("Health:StaleHours")
                            ?? VarsayilanBayatlikSaati;

        // GET *ve* HEAD — ikisi birden ZORUNLU.
        //
        // UptimeRobot (ve birçok izleme aracı) HTTP monitörlerinde varsayılan
        // olarak HEAD atıyor. MapGet'e gelen HEAD isteğini ASP.NET Core
        // karşılamıyor ve 405 dönüyor; izleyici bunu "down" sayıyor, üstelik
        // gövde olmadığı için yanıt süresi bile ölçülemiyor. Canlıda tam bu
        // oldu: uç GET ile 200 dönerken monitör sürekli kırmızıydı ve sorun
        // izleyicide sanıldı. HEAD'de gövde gönderilmiyor ama durum kodu aynı,
        // yani 200/503 ayrımı korunuyor — izleme için gereken de bu.
        app.MapMethods("/api/health/sources", ["GET", "HEAD"], async (AppDbContext db, CancellationToken ct) =>
        {
            var simdi = DateTimeOffset.UtcNow;
            var bayatlikSiniri = simdi.AddHours(-bayatlikSaati);
            var emeklilikSiniri = simdi.AddDays(-EmekliGunu);

            // Kaynak = ürünü kim getiriyor. Bayi ürünlerinde satıcı, markanın
            // kendi sitesinden gelenlerde markanın kendisi. Satıcıya göre
            // gruplamak tek başına yetmezdi: bayilerden gelmeyen ~50 markanın
            // scraper'ı tek tek bozulabilir ve hepsi "markanın kendi sitesi"
            // adlı tek bir kovaya düşerdi, biri çalıştığı sürece arıza görünmezdi.
            var kaynaklar = await db.Products
                .Where(p => p.LatestScrapedAt != null)
                .GroupBy(p => p.Seller ?? p.Brand!.Name)
                .Select(g => new
                {
                    Kaynak = g.Key,
                    SonTarama = g.Max(p => p.LatestScrapedAt)!.Value,
                    UrunSayisi = g.Count(),
                })
                .ToListAsync(ct);

            var bayat = kaynaklar
                .Where(k => k.SonTarama < bayatlikSiniri && k.SonTarama >= emeklilikSiniri)
                .OrderBy(k => k.SonTarama)
                .Select(k => new
                {
                    kaynak = k.Kaynak,
                    sonTarama = k.SonTarama,
                    saatOnce = (int)(simdi - k.SonTarama).TotalHours,
                    urunSayisi = k.UrunSayisi,
                })
                .ToList();

            var emekli = kaynaklar
                .Where(k => k.SonTarama < emeklilikSiniri)
                .Select(k => k.Kaynak)
                .OrderBy(a => a)
                .ToList();

            var govde = new
            {
                durum = bayat.Count == 0 ? "saglikli" : "bayat-kaynak-var",
                esikSaat = bayatlikSaati,
                kaynakSayisi = kaynaklar.Count,
                bayatKaynaklar = bayat,
                // Bilgi amaçlı: 503 üretmiyorlar (yukarıdaki açıklamaya bak).
                emekliKaynaklar = emekli,
            };

            // Sağlık ucu ÖNBELLEKLENMEMELİ — önbelleklenmiş bir "sağlıklı"
            // yanıtı arızayı gizler. Çıktı önbelleği politikası zaten
            // uygulanmıyor; bu başlık Cloudflare ve aradaki her katman için.
            return bayat.Count == 0
                ? Results.Json(govde, statusCode: StatusCodes.Status200OK)
                : Results.Json(govde, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
    }
}

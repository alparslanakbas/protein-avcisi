using System.Globalization;
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

    /// <summary>
    /// Bilinen bir arızayı bitiş gününe kadar (UTC, gün DAHİL) 503'ten çıkaran
    /// kayıt: <c>Health:Susturulan</c> altında <c>Kaynak</c> + <c>Bitis</c>
    /// ("yyyy-MM-dd"). Kaynak adı gövdedeki <c>kaynak</c> alanıyla BİREBİR aynı
    /// yazılmalı.
    ///
    /// <b>NEDEN VAR.</b> Tek bir kaynak yüzünden kırmızı kalan monitör
    /// KÖRLEŞİYOR: zaten "down" olduğu için ikinci bir kaynak bozulunca yeni
    /// haber gelmiyor. 2 Ekim'de yaşandı: Gigi's sitesini Shopify'a taşıdı ve uç
    /// 30 günlük emekliliğe kadar 503 verecekti; aynı gece sunucunun IP'si
    /// Shopify'ın 429 sınırına takıldı, başka mağazalar bozulsa haber gelmeyecekti.
    ///
    /// <b>Bitiş ZORUNLU:</b> süresiz susturma unutulur ve kaynağı sessizce
    /// izlemeden çıkarır; tarih geçince kaynak kendiliğinden yeniden alarm
    /// üretiyor. Susturulan kaynak gövdede ayrı listede görünmeye devam ediyor.
    /// Adı ya da tarihi bozuk kayıt YOK SAYILIYOR, uygulamayı durdurmuyor: güvenli
    /// yön alarmın çalmaya devam etmesi. Üretim ayarındaki kayıtların geçerli
    /// olduğunu bir test sınıyor (yazım hatası "susturdum" sanılan alarmı açık
    /// bırakmasın diye).
    /// </summary>
    internal sealed record KaynakSusturma(string Kaynak, DateOnly Bitis);

    internal sealed record KaynakDurumu(string Kaynak, DateTimeOffset SonTarama, int UrunSayisi);

    internal sealed record SusturulanKaynak(KaynakDurumu Kaynak, DateOnly Bitis);

    internal sealed record SaglikSiniflamasi(
        IReadOnlyList<KaynakDurumu> Bayat,
        IReadOnlyList<SusturulanKaynak> Susturulan,
        IReadOnlyList<string> Emekli);

    internal static IReadOnlyList<KaynakSusturma> SusturmalariOku(IConfiguration yapilandirma) =>
        yapilandirma.GetSection("Health:Susturulan").GetChildren()
            .Select(k => (Kaynak: k["Kaynak"]?.Trim(), Bitis: k["Bitis"]))
            .Where(k => !string.IsNullOrEmpty(k.Kaynak))
            .Select(k => DateOnly.TryParseExact(k.Bitis, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var bitis)
                ? new KaynakSusturma(k.Kaynak!, bitis)
                : null)
            .OfType<KaynakSusturma>()
            .ToList();

    /// <summary>
    /// Kaynakları ayırır: 503 üreten bayatlar, süreli susturulmuş bayatlar ve
    /// emekliler. Uçtan ayrı ve saf, alarmın kararı veritabanısız sınanabilsin
    /// diye: sessizce yeşil kalan bir alarm, hiç olmamasından beter.
    /// </summary>
    internal static SaglikSiniflamasi Siniflandir(
        IEnumerable<KaynakDurumu> kaynaklar, DateTimeOffset simdi, int bayatlikSaati,
        IReadOnlyList<KaynakSusturma> susturmalar)
    {
        var bayatlikSiniri = simdi.AddHours(-bayatlikSaati);
        var emeklilikSiniri = simdi.AddDays(-EmekliGunu);
        var bugun = DateOnly.FromDateTime(simdi.UtcDateTime);

        var bayat = new List<KaynakDurumu>();
        var susturulan = new List<SusturulanKaynak>();
        var emekli = new List<string>();

        foreach (var kaynak in kaynaklar.OrderBy(k => k.SonTarama))
        {
            if (kaynak.SonTarama < emeklilikSiniri)
            {
                emekli.Add(kaynak.Kaynak);
                continue;
            }

            if (kaynak.SonTarama >= bayatlikSiniri)
                continue;

            // Aynı kaynağa birden çok kayıt yazılmışsa en geç biten geçerli.
            var susturma = susturmalar
                .Where(s => s.Kaynak == kaynak.Kaynak && bugun <= s.Bitis)
                .MaxBy(s => s.Bitis);

            if (susturma is null)
                bayat.Add(kaynak);
            else
                susturulan.Add(new SusturulanKaynak(kaynak, susturma.Bitis));
        }

        return new SaglikSiniflamasi(bayat, susturulan, emekli.OrderBy(a => a).ToList());
    }

    public static void MapHealthEndpoints(this WebApplication app)
    {
        var bayatlikSaati = app.Configuration.GetValue<int?>("Health:StaleHours")
                            ?? VarsayilanBayatlikSaati;
        var susturmalar = SusturmalariOku(app.Configuration);

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

            // Kaynak = ürünü kim getiriyor. Bayi ürünlerinde satıcı, markanın
            // kendi sitesinden gelenlerde markanın kendisi. Satıcıya göre
            // gruplamak tek başına yetmezdi: bayilerden gelmeyen ~50 markanın
            // scraper'ı tek tek bozulabilir ve hepsi "markanın kendi sitesi"
            // adlı tek bir kovaya düşerdi, biri çalıştığı sürece arıza görünmezdi.
            // IgnoreQueryFilters: gizlenmis urunler de TARANMAYA devam ediyor
            // (yutma servisi filtreyi atliyor). Bu uc "tarama hala calisiyor
            // mu" sorusunu cevapladigi icin gerceği yansitmali; aksi halde bir
            // kaynagin butun urunleri gizlense o kaynak listeden dusup alarm
            // uretemez hale gelirdi.
            var kaynaklar = await db.Products
                .IgnoreQueryFilters()
                .Where(p => p.LatestScrapedAt != null)
                .GroupBy(p => p.Seller ?? p.Brand!.Name)
                .Select(g => new
                {
                    Kaynak = g.Key,
                    SonTarama = g.Max(p => p.LatestScrapedAt)!.Value,
                    UrunSayisi = g.Count(),
                })
                .ToListAsync(ct);

            var sonuc = Siniflandir(
                kaynaklar.Select(k => new KaynakDurumu(k.Kaynak, k.SonTarama, k.UrunSayisi)),
                simdi, bayatlikSaati, susturmalar);

            var govde = new
            {
                durum = sonuc.Bayat.Count == 0 ? "saglikli" : "bayat-kaynak-var",
                esikSaat = bayatlikSaati,
                kaynakSayisi = kaynaklar.Count,
                bayatKaynaklar = sonuc.Bayat.Select(k => new
                {
                    kaynak = k.Kaynak,
                    sonTarama = k.SonTarama,
                    saatOnce = (int)(simdi - k.SonTarama).TotalHours,
                    urunSayisi = k.UrunSayisi,
                }).ToList(),
                // Bilinen arıza: bitiş gününe kadar 503 üretmiyor (KaynakSusturma).
                susturulanKaynaklar = sonuc.Susturulan.Select(s => new
                {
                    kaynak = s.Kaynak.Kaynak,
                    sonTarama = s.Kaynak.SonTarama,
                    saatOnce = (int)(simdi - s.Kaynak.SonTarama).TotalHours,
                    urunSayisi = s.Kaynak.UrunSayisi,
                    bitis = s.Bitis,
                }).ToList(),
                // Bilgi amaçlı: 503 üretmiyorlar (yukarıdaki açıklamaya bak).
                emekliKaynaklar = sonuc.Emekli,
            };

            // Sağlık ucu ÖNBELLEKLENMEMELİ — önbelleklenmiş bir "sağlıklı"
            // yanıtı arızayı gizler. Çıktı önbelleği politikası zaten
            // uygulanmıyor; bu başlık Cloudflare ve aradaki her katman için.
            return sonuc.Bayat.Count == 0
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

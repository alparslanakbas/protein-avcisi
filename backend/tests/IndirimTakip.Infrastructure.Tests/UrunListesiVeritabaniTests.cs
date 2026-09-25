using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IndirimTakip.Infrastructure.Tests;

/// <summary>
/// Yalnızca <c>TEST_DB</c> tanımlıysa çalışan test. Yerelde tanımlı değilse
/// "atlandı" olarak GÖRÜNÜR (sessizce geçmiyor). CI'da ATLANMIYOR: orada
/// değişken kaybolursa test çalışıp düşüyor, yoksa kapı bu testi hiç koşmadan
/// yeşil yanardı (geçmiş sayılan ama hiçbir şey ölçmeyen test, olmayan
/// testten beterdir).
/// </summary>
public sealed class VeritabaniFactAttribute : FactAttribute
{
    public VeritabaniFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_DB"))
            && Environment.GetEnvironmentVariable("CI") != "true")
            Skip = "TEST_DB tanımlı değil; gerçek veritabanı testi atlandı.";
    }
}

/// <summary>
/// Boş bir test veritabanını göç ettirip küçük ama tuzaklı bir katalogla
/// dolduruyor. Veritabanı adında "test" geçmiyorsa HİÇBİR ŞEY YAPMIYOR:
/// başlangıçta veritabanı silinip yeniden kuruluyor, yanlış bağlantı dizesi
/// gerçek veriyi götürürdü.
/// </summary>
public sealed class UrunListesiVeritabani : IAsyncLifetime
{
    public string? Baglanti { get; } = Environment.GetEnvironmentVariable("TEST_DB");

    /// <summary>Süzgeçsiz listede görünmesi gereken ürün sayısı.</summary>
    public int GorunenUrun { get; private set; }

    /// <summary>Gerçek indirimli (son fiyat &lt; 30 günlük referans) ürün sayısı.</summary>
    public int IndirimliUrun { get; private set; }

    public AppDbContext Baglam() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Baglanti).Options);

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Baglanti))
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("CI'da TEST_DB tanımlı değil; veritabanı testleri koşamaz.");
            return;
        }

        var ad = new NpgsqlConnectionStringBuilder(Baglanti).Database ?? string.Empty;
        if (!ad.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"TEST_DB '{ad}' veritabanını gösteriyor; adında 'test' geçmeli.");

        await using var db = Baglam();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DoldurAsync(db);
        await new PriceSummaryRefresher(db, NullLogger<PriceSummaryRefresher>.Instance).RefreshAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task DoldurAsync(AppDbContext db)
    {
        var simdi = DateTimeOffset.UtcNow;
        var hardline = new Brand { Name = "Hardline", BaseUrl = "https://www.hardline.com.tr" };
        var olimp = new Brand { Name = "Olimp", BaseUrl = "https://protein7.com" };
        var swiss = new Brand { Name = "Swiss Nutrition", BaseUrl = "https://swissnutrition.com.tr" };
        var pasifMarka = new Brand { Name = "Gizli Marka", BaseUrl = "https://gizli.example", IsActive = false };
        db.Brands.AddRange(hardline, olimp, swiss, pasifMarka);

        var sira = 0;
        Product Urun(Brand marka, string ad, string? kategori, string? satici, params decimal[] fiyatlar)
        {
            sira++;
            var urun = new Product
            {
                Brand = marka,
                Name = ad,
                Url = $"https://kaynak.example/{sira}",
                Category = kategori,
                Seller = satici,
            };
            // Fiyatlar eskiden yeniye; son fiyat bugün, öncekiler birer gün arayla.
            for (var i = 0; i < fiyatlar.Length; i++)
            {
                urun.PriceHistories.Add(new PriceHistory
                {
                    Price = fiyatlar[i],
                    ScrapedAt = simdi.AddDays(-(fiyatlar.Length - 1 - i)).AddMinutes(-1),
                });
            }
            db.Products.Add(urun);
            return urun;
        }

        // Görünen ürünler. Adlar bilerek tuzaklı: Türkçe İ/ı, aynı ad iki
        // satıcıda (sayfalamada eşitlik bozucu olmazsa sıra oynar), boşluklu
        // marka ("protein ocean" aramasının kardeşi).
        for (var i = 1; i <= 8; i++)
            Urun(hardline, $"Hardline Whey 3 Matrix {i * 100} gr", "protein-tozu", null, 1000m + i, 900m + i);
        for (var i = 1; i <= 5; i++)
            Urun(hardline, $"Hardline Kreatin {i}", "kreatin", null, 500m);
        Urun(hardline, "VİTAMİN D3 1000 IU", "vitamin", null, 300m, 250m);
        Urun(hardline, "Vitamin C", "vitamin", null, 200m);
        Urun(hardline, "Işıl Amino", "amino-asitler", null, 400m, 450m);

        // Aynı ad, aynı fiyat, iki bayi: sıralama anahtarları birebir eşit.
        for (var i = 0; i < 4; i++)
        {
            Urun(olimp, "Olimp Whey Protein Complex 2270 gr", "protein-tozu", "protein7.com", 2500m, 2400m);
            Urun(olimp, "Olimp Whey Protein Complex 2270 gr", "protein-tozu", "provitamin.com.tr", 2500m, 2400m);
        }
        Urun(swiss, "Swiss Nutrition Kolajen", null, null, 700m);
        Urun(swiss, "Swiss Nutrition Magnezyum", "vitamin", "swissnutrition.com.tr", 150m, 120m);

        GorunenUrun = sira;
        IndirimliUrun = 8 + 1 + 8 + 1; // whey'ler, D3, Olimp'ler, magnezyum

        // GÖRÜNMEMESİ gerekenler.
        var bayat = Urun(hardline, "Bayat Ürün", "protein-tozu", null, 999m);
        foreach (var f in bayat.PriceHistories)
            f.ScrapedAt = simdi.AddDays(-5);
        var pasif = Urun(hardline, "Pasif Ürün", "protein-tozu", null, 999m);
        pasif.IsActive = false;
        Urun(pasifMarka, "Gizli Marka Ürünü", "protein-tozu", null, 999m);

        await db.SaveChangesAsync();
    }
}

/// <summary>
/// <see cref="DealsQueryService.GetDealsAsync"/> gerçek PostgreSQL'e karşı
/// (güvenlik/mimari incelemesi, 26 Eylül, bulgu 11). Bu sınıf bu depoda iki
/// kez canlıyı düşürdü ve hataların ikisi de SQL'e çevirme aşamasındaydı:
/// ne derleme ne bellek içi test onları görebilir. Asıl kontrol sayfalama:
/// bütün sayfalar dolaşılınca her ürün TAM BİR KEZ gelmeli.
/// </summary>
public class UrunListesiVeritabaniTests(UrunListesiVeritabani veri) : IClassFixture<UrunListesiVeritabani>
{
    private static readonly string?[] Siralamalar =
        [null, "name_asc", "name_desc", "price_asc", "price_desc", "newest", "oldest"];

    private static readonly string[]?[] Saticilar =
        [null, [DealsQueryService.BrandDirectSellerLabel], [DealsQueryService.DealerSellerLabel]];

    private static readonly string?[] Aramalar = [null, "whey", "vitamin", "VİTAMİN", "ışıl", "hardline whey"];

    private DealsQueryService Servis(AppDbContext db) =>
        new(db, Options.Create(new AffiliateOptions()), new ProductImageOptions());

    private static Task<PagedResult<DealDto>> Getir(
        DealsQueryService servis, string? siralama, string[]? saticilar, string? arama,
        bool indirimli, int sayfa, int boyut, bool magazaIndirimi = false) =>
        servis.GetDealsAsync(
            PriceSummaryRefresher.WindowDays, null, null, saticilar, arama, null, null,
            indirimli, magazaIndirimi, siralama, sayfa, boyut);

    [VeritabaniFact]
    public async Task Butun_sayfalar_her_urunu_tam_bir_kez_veriyor()
    {
        await using var db = veri.Baglam();
        var servis = Servis(db);
        var denenen = 0;

        foreach (var siralama in Siralamalar)
        foreach (var saticilar in Saticilar)
        foreach (var arama in Aramalar)
        foreach (var indirimli in new[] { false, true })
        {
            var ilk = await Getir(servis, siralama, saticilar, arama, indirimli, 1, 4);
            var gorulen = new List<int>();
            for (var sayfa = 1; sayfa <= Math.Max(ilk.TotalPages, 1); sayfa++)
            {
                var sonuc = sayfa == 1 ? ilk : await Getir(servis, siralama, saticilar, arama, indirimli, sayfa, 4);
                Assert.True(sonuc.Items.Count <= 4);
                gorulen.AddRange(sonuc.Items.Select(d => d.ProductId));
            }

            var durum = $"sıralama={siralama}, satıcı={saticilar?[0]}, arama={arama}, indirimli={indirimli}";
            Assert.True(gorulen.Count == gorulen.Distinct().Count(), $"Tekrarlayan ürün: {durum}");
            Assert.True(gorulen.Count == ilk.TotalCount, $"Eksik ürün ({gorulen.Count}/{ilk.TotalCount}): {durum}");

            // Aynı istek aynı sırayı vermeli.
            var tekrar = await Getir(servis, siralama, saticilar, arama, indirimli, 1, 4);
            Assert.Equal(ilk.Items.Select(d => d.ProductId), tekrar.Items.Select(d => d.ProductId));
            denenen++;
        }

        Assert.Equal(Siralamalar.Length * Saticilar.Length * Aramalar.Length * 2, denenen);
    }

    [VeritabaniFact]
    public async Task Suzgecsiz_liste_bayat_pasif_ve_gizli_markayi_gostermiyor()
    {
        await using var db = veri.Baglam();

        var sonuc = await Getir(Servis(db), null, null, null, false, 1, 100);

        Assert.Equal(veri.GorunenUrun, sonuc.TotalCount);
        Assert.DoesNotContain(sonuc.Items, d => d.ProductName is "Bayat Ürün" or "Pasif Ürün" or "Gizli Marka Ürünü");
    }

    [VeritabaniFact]
    public async Task Indirimli_suzgeci_yalnizca_gercek_indirimi_veriyor()
    {
        await using var db = veri.Baglam();

        var sonuc = await Getir(Servis(db), null, null, null, true, 1, 100);

        Assert.Equal(veri.IndirimliUrun, sonuc.TotalCount);
        Assert.All(sonuc.Items, d => Assert.True(d.DiscountPercent > 0, d.ProductName));
    }

    /// <summary>
    /// Türkçe İ tuzağı SQL tarafında: "VİTAMİN" ve "vitamin" aynı ürünleri
    /// bulmalı, noktalı İ ile yazılmış ad dahil.
    /// </summary>
    [VeritabaniFact]
    public async Task Turkce_buyuk_harfli_arama_kucuk_harfliyle_ayni()
    {
        await using var db = veri.Baglam();
        var servis = Servis(db);

        var buyuk = await Getir(servis, "name_asc", null, "VİTAMİN", false, 1, 100);
        var kucuk = await Getir(servis, "name_asc", null, "vitamin", false, 1, 100);

        Assert.Equal(kucuk.Items.Select(d => d.ProductId), buyuk.Items.Select(d => d.ProductId));
        Assert.Contains(buyuk.Items, d => d.ProductName == "VİTAMİN D3 1000 IU");
    }

    [VeritabaniFact]
    public async Task Bayi_suzgeci_yalnizca_bayi_kayitlarini_veriyor()
    {
        await using var db = veri.Baglam();

        var bayi = await Getir(Servis(db), null, [DealsQueryService.DealerSellerLabel], null, false, 1, 100);
        var kendi = await Getir(Servis(db), null, [DealsQueryService.BrandDirectSellerLabel], null, false, 1, 100);

        Assert.All(bayi.Items, d => Assert.NotNull(d.Seller));
        Assert.All(kendi.Items, d => Assert.Null(d.Seller));
        Assert.Equal(veri.GorunenUrun, bayi.TotalCount + kendi.TotalCount);
    }
}

using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Images;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        // Gerçek indirim: önceki fiyat en az bir hafta (olağan fiyat), sonra düşüş.
        static decimal[] Hafta(decimal onceki, decimal son) =>
            [.. Enumerable.Repeat(onceki, PriceSummaryRefresher.OlaganGunSayisi), son];

        // Görünen ürünler. Adlar bilerek tuzaklı: Türkçe İ/ı, aynı ad iki
        // satıcıda (sayfalamada eşitlik bozucu olmazsa sıra oynar), boşluklu
        // marka ("protein ocean" aramasının kardeşi).
        for (var i = 1; i <= 8; i++)
            Urun(hardline, $"Hardline Whey 3 Matrix {i * 100} gr", "protein-tozu", null, Hafta(1000m + i, 900m + i));
        for (var i = 1; i <= 5; i++)
            Urun(hardline, $"Hardline Kreatin {i}", "kreatin", null, 500m);
        Urun(hardline, "VİTAMİN D3 1000 IU", "vitamin", null, Hafta(300m, 250m));
        Urun(hardline, "Vitamin C", "vitamin", null, 200m);
        Urun(hardline, "Işıl Amino", "amino-asitler", null, 400m, 450m);

        // Görünen ama İNDİRİMLİ OLMAYANLAR (3 Ekim kuralı): önceki fiyat bir
        // haftadan kısa görüldü. Eski hesap (30 günün en yükseği) ikisini de
        // indirimli sayıyordu; sıçramalı olan %75 gösterirdi.
        Urun(hardline, "Kısa Geçmişli Düşüş", "kreatin", null, 300m, 250m);
        Urun(hardline, "Sıçramalı Kreatin", "kreatin", null,
            [.. Enumerable.Repeat(500m, 8), 2000m, 2000m, 500m]);

        // Boşluk kuralı (6 Ekim): sekiz gün 4.150, sonra altı gün görünmüyor, sonra üç gün 3.526. Eski hesap
        // olağan fiyatı 4.150 sayıp %15 "gerçek indirim" veriyordu; aradan sonra olağan fiyat yok, indirim 0.
        var donen = Urun(hardline, "Boşluktan Dönen", "protein-tozu", null,
            [.. Enumerable.Repeat(4150m, 8), 3526m, 3526m, 3526m]);
        foreach (var f in donen.PriceHistories.Where(f => f.Price == 4150m))
            f.ScrapedAt = f.ScrapedAt.AddDays(-5);
        // Kontrol: iki günlük ara eşiğin altında (günde bir taranan kaynakta kaçan tur), indirim duruyor.
        var kisaAra = Urun(hardline, "Kısa Aralı Düşüş", "protein-tozu", null, Hafta(1000m, 900m));
        foreach (var f in kisaAra.PriceHistories.Where(f => f.Price == 1000m))
            f.ScrapedAt = f.ScrapedAt.AddDays(-1);

        // Aynı ad, aynı fiyat, iki bayi: sıralama anahtarları birebir eşit.
        for (var i = 0; i < 4; i++)
        {
            Urun(olimp, "Olimp Whey Protein Complex 2270 gr", "protein-tozu", "protein7.com", Hafta(2500m, 2400m));
            Urun(olimp, "Olimp Whey Protein Complex 2270 gr", "protein-tozu", "provitamin.com.tr", Hafta(2500m, 2400m));
        }
        Urun(swiss, "Swiss Nutrition Kolajen", null, null, 700m);
        Urun(swiss, "Swiss Nutrition Magnezyum", "vitamin", "swissnutrition.com.tr", Hafta(150m, 120m));

        // Sezon sayfasının yönetmelik ölçütü (KampanyaIndirimServisi): mağaza hepsinde üstü çizili eski fiyat
        // gösteriyor. Ayrı marka ve kategori, başka testlerin sayılarına karışmasın diye.
        var kampanya = new Brand { Name = "Kampanya Markası", BaseUrl = "https://kampanya.example" };
        db.Brands.Add(kampanya);
        void EskiFiyat(Product urun, decimal eski)
        {
            foreach (var f in urun.PriceHistories)
                f.StoreOldPrice = eski;
        }
        EskiFiyat(Urun(kampanya, "Kampanya Gerçek", "kilo-hacim", null, [.. Enumerable.Repeat(1000m, 10), 850m, 850m]), 1000m);
        // Tek günlük 500 TL okuma hatası karşılaştırma fiyatı sayılmamalı (en az iki günde görülen en düşük: 1000).
        EskiFiyat(Urun(kampanya, "Kampanya Tek Gün Hatalı", "kilo-hacim", null,
            [.. Enumerable.Repeat(1000m, 5), 500m, .. Enumerable.Repeat(1000m, 4), 950m, 950m]), 1100m);
        EskiFiyat(Urun(kampanya, "Kampanya Kalıcı", "kilo-hacim", null, [.. Enumerable.Repeat(800m, 35)]), 1200m);
        EskiFiyat(Urun(kampanya, "Kampanya Ucuzlamamış", "kilo-hacim", null, [.. Enumerable.Repeat(700m, 10), 900m, 900m, 900m]), 1200m);
        EskiFiyat(Urun(kampanya, "Kampanya Ürün Değişmiş", "kilo-hacim", null, [.. Enumerable.Repeat(300m, 10), 900m, 900m, 900m]), 1500m);
        EskiFiyat(Urun(kampanya, "Kampanya Kısa Geçmiş", "kilo-hacim", null, 600m, 600m, 600m), 900m);
        Urun(kampanya, "Kampanya Beyansız", "kilo-hacim", null, 500m, 500m);

        GorunenUrun = sira;
        // whey'ler, D3, Olimp'ler, magnezyum, kısa aralı düşüş; kampanyadan gerçek ve tek gün hatalı (olağan fiyat 1000)
        IndirimliUrun = 8 + 1 + 8 + 1 + 1 + 2;

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
        Assert.DoesNotContain(sonuc.Items, d => d.ProductName is "Kısa Geçmişli Düşüş" or "Sıçramalı Kreatin");
    }

    // Fiyat geçmişinde eşikten uzun aradan sonra dönen ürün olağan fiyatını yeniden kazanmalı; kısa ara (kaçan tur)
    // indirimi silmemeli.
    [VeritabaniFact]
    public async Task Bosluktan_donen_urunun_olagan_fiyati_aradan_sonrasindan()
    {
        await using var db = veri.Baglam();

        var sonuc = await Getir(Servis(db), null, null, null, false, 1, 100);

        var donen = Assert.Single(sonuc.Items, d => d.ProductName == "Boşluktan Dönen");
        Assert.Equal(0m, donen.DiscountPercent);
        Assert.Equal(3526m, donen.ReferencePrice);
        var kisaAra = Assert.Single(sonuc.Items, d => d.ProductName == "Kısa Aralı Düşüş");
        Assert.Equal(1000m, kisaAra.ReferencePrice);
    }

    // Favoriler son fiyatı ürün başına ayrı sorguyla değil, tek toplu sorguyla alıyor (6 Ekim; projeksiyondaki
    // FirstOrDefault bütün fiyat geçmişini pencereliyordu). Her ürün, ürün sayfasıyla aynı son fiyatı vermeli.
    [VeritabaniFact]
    public async Task Favoriler_her_urunde_urun_sayfasiyla_ayni_son_fiyati_veriyor()
    {
        await using var db = veri.Baglam();
        var servis = Servis(db);
        var idler = await db.Products.Select(p => p.Id).ToListAsync();

        var favoriler = await servis.GetDealsByIdsAsync(idler);

        Assert.True(favoriler.Count >= veri.GorunenUrun, $"{favoriler.Count} ürün döndü");
        foreach (var f in favoriler)
        {
            var urun = await servis.GetProductByIdAsync(f.ProductId);
            Assert.NotNull(urun);
            Assert.Equal((urun.CurrentPrice, urun.ScrapedAt, urun.ReferencePrice, urun.DiscountPercent),
                (f.CurrentPrice, f.ScrapedAt, f.ReferencePrice, f.DiscountPercent));
        }
    }

    // Sezon sayfası ve rapor yazısı (7 Ekim): mağazanın indirim dediği ürünler yönetmelik ölçütüyle. Tek günlük
    // hatalı düşük fiyat karşılaştırma fiyatı sayılmıyor, iki kattan büyük fark ürün değişikliği, bir aydan uzun
    // aynı fiyat kalıcı; mağaza eski fiyat göstermiyorsa ürün hiç sayılmıyor.
    [VeritabaniFact]
    public async Task Kampanya_ozeti_yonetmelik_olcutuyle_siniflandiriyor()
    {
        await using var db = veri.Baglam();

        var ozet = await new KampanyaIndirimServisi(db, Servis(db)).OzetAsync();

        Assert.Equal(6, ozet.MagazaIndirimDiyor);
        Assert.Equal((2, 1, 1, 1, 1), (ozet.Gercek, ozet.Ucuzlamamis, ozet.Kalici, ozet.VeriYetersiz, ozet.UrunDegismisOlabilir));
        Assert.Equal(
            [("Kampanya Gerçek", 1000m, 15.0m), ("Kampanya Tek Gün Hatalı", 1000m, 5.0m)],
            ozet.GercekIndirimler.Select(k => (k.Urun.ProductName, k.OncekiEnDusuk, k.GercekYuzde)));
    }

    // Olağan fiyatı olmayan ürün listede kalıyor, indirimi 0: referans güncel
    // fiyat. Referans NULL olsaydı liste sorgusu ürünü tamamen düşürürdü.
    [VeritabaniFact]
    public async Task Kisa_gecmisli_urun_listede_kalir_indirimi_sifir()
    {
        await using var db = veri.Baglam();

        var sonuc = await Getir(Servis(db), null, null, null, false, 1, 100);

        var kisa = Assert.Single(sonuc.Items, d => d.ProductName == "Kısa Geçmişli Düşüş");
        Assert.Equal(0m, kisa.DiscountPercent);
        Assert.Equal(kisa.CurrentPrice, kisa.ReferencePrice);
        var sicrama = Assert.Single(sonuc.Items, d => d.ProductName == "Sıçramalı Kreatin");
        Assert.Equal(500m, sicrama.ReferencePrice);
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

    // Ürün sayfası, favoriler ve istatistikler referansı kendileri (pencerenin
    // en yükseği) hesaplıyordu: 3 Ekim'in ilk sürümünde listeler olağan fiyata
    // geçti, ürün sayfası sıçramalı ürünün eski yüzdesini göstermeye devam etti.
    // Aynı ürün her yerde aynı referansı göstermeli.
    [VeritabaniFact]
    public async Task Urun_sayfasi_favoriler_ve_istatistikler_listeyle_ayni_referansi_kullanir()
    {
        await using var db = veri.Baglam();
        var servis = Servis(db);
        var sicramali = await db.Products.Where(p => p.Name == "Sıçramalı Kreatin").Select(p => p.Id).SingleAsync();
        var kisa = await db.Products.Where(p => p.Name == "Kısa Geçmişli Düşüş").Select(p => p.Id).SingleAsync();

        var urun = await servis.GetProductByIdAsync(sicramali);
        Assert.NotNull(urun);
        Assert.Equal(500m, urun.ReferencePrice);
        Assert.Equal(0m, urun.DiscountPercent);

        var favoriler = await servis.GetDealsByIdsAsync([sicramali, kisa]);
        Assert.Equal(2, favoriler.Count);
        Assert.All(favoriler, d => Assert.Equal(0m, d.DiscountPercent));

        var katalog = new CatalogStatsQueryService(db);
        Assert.Equal(veri.IndirimliUrun, (await katalog.GetHomepageStatsAsync()).DiscountCount);
        Assert.Equal(8 + 1 + 1, (await katalog.GetBrandStatsAsync("Hardline")).DiscountCount); // whey'ler, D3, kısa aralı (boşluktan dönen değil)
    }

    // Yönetim paneli abone listesi (4 Ekim): sayfa, durum süzgeci ve e-posta araması
    // sunucuda. Test kendi abonelerini "liste-" önekiyle ekliyor; öteki testlerin
    // kayıtları sayıları etkilemesin.
    [VeritabaniFact]
    public async Task Abone_listesi_sayfalaniyor_suzuluyor_ve_araniyor()
    {
        await using var db = veri.Baglam();
        var simdi = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 12; i++)
        {
            // i % 3: 0 aktif, 1 onay bekliyor, 2 ayrıldı — her durumdan dört kayıt.
            db.Subscribers.Add(new Subscriber
            {
                Email = $"liste-{i:00}@ornek.test",
                Token = Guid.NewGuid().ToString("N"),
                IsConfirmed = i % 3 == 0,
                SubscribedAt = simdi.AddMinutes(-i),
                UnsubscribedAt = i % 3 == 2 ? simdi : null,
            });
        }
        await db.SaveChangesAsync();
        var servis = AboneServisi(db);

        var ilk = await servis.ListForAdminAsync("liste-", null, 1, 10);
        var ikinci = await servis.ListForAdminAsync("liste-", null, 2, 10);
        Assert.Equal(12, ilk.Toplam);
        Assert.Equal(10, ilk.Aboneler.Count);
        Assert.Equal(2, ikinci.Aboneler.Count);
        Assert.Equal("liste-01@ornek.test", ilk.Aboneler[0].Email); // en yeni önce
        Assert.Equal(12, ilk.Aboneler.Concat(ikinci.Aboneler).Select(a => a.Id).Distinct().Count());

        foreach (var durum in new[] { SubscriberStatus.Active, SubscriberStatus.Pending, SubscriberStatus.Unsubscribed })
        {
            var suzulen = await servis.ListForAdminAsync("liste-", durum, 1, 50);
            Assert.Equal(4, suzulen.Toplam);
            Assert.All(suzulen.Aboneler, a => Assert.Equal(durum, a.Durum));
        }

        // Büyük harfle arama da bulmalı.
        var tek = await servis.ListForAdminAsync("LISTE-07", null, 1, 50);
        Assert.Equal("liste-07@ornek.test", Assert.Single(tek.Aboneler).Email);
    }

    // Kalıcı silme: takip listesi ve favoriler cascade ile gidiyor, başka abonenin
    // kayıtlarına dokunulmuyor. Kısıt veritabanında (migration); kodda değil.
    [VeritabaniFact]
    public async Task Abone_silinince_takip_ve_favorileri_de_gider()
    {
        await using var db = veri.Baglam();
        var urun = await db.Products.OrderBy(p => p.Id).FirstAsync();
        Subscriber Yeni(string email) => new()
        {
            Email = email, Token = Guid.NewGuid().ToString("N"), IsConfirmed = true, SubscribedAt = DateTimeOffset.UtcNow,
        };
        var trol = Yeni("silme-trol@ornek.test");
        var gercek = Yeni("silme-gercek@ornek.test");
        db.Subscribers.AddRange(trol, gercek);
        await db.SaveChangesAsync();
        db.ProductWatches.AddRange(
            new ProductWatch { SubscriberId = trol.Id, ProductId = urun.Id, CreatedAt = DateTimeOffset.UtcNow },
            new ProductWatch { SubscriberId = gercek.Id, ProductId = urun.Id, CreatedAt = DateTimeOffset.UtcNow });
        db.ProductFavorites.Add(new ProductFavorite { SubscriberId = trol.Id, ProductId = urun.Id, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var servis = AboneServisi(db);
        Assert.True(await servis.DeleteAsync(trol.Id));
        Assert.False(await servis.DeleteAsync(trol.Id)); // ikinci kez: artık yok

        await using var kontrol = veri.Baglam();
        Assert.False(await kontrol.Subscribers.AnyAsync(s => s.Id == trol.Id));
        Assert.False(await kontrol.ProductWatches.IgnoreQueryFilters().AnyAsync(w => w.SubscriberId == trol.Id));
        Assert.False(await kontrol.ProductFavorites.IgnoreQueryFilters().AnyAsync(f => f.SubscriberId == trol.Id));
        Assert.True(await kontrol.ProductWatches.IgnoreQueryFilters().AnyAsync(w => w.SubscriberId == gercek.Id));
    }

    private static SubscriberService AboneServisi(AppDbContext db) =>
        new(db, new GonderilmeyenEposta(), new ConfigurationBuilder().Build(), NullLogger<SubscriberService>.Instance);

    // Listeleme ve silme e-posta göndermez; gönderirse test bunu yakalasın.
    private sealed class GonderilmeyenEposta : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Bu testte e-posta gönderilmemeli.");
    }
}

using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Deals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace IndirimTakip.Infrastructure.Tests;

// Aylık fiyat endeksinin saf kısmı: zincirleme, ay sınırı, CSV. SQL aşağıdaki veritabanı testinde.
public class FiyatEndeksiTests
{
    private static PriceIndexMonth Satir(int yil, int ay, string kategori, decimal oran, bool gecici = false) => new()
    {
        Month = new DateOnly(yil, ay, 1),
        Category = kategori,
        Ratio = oran,
        ProductCount = 100,
        BrandCount = 10,
        RisenCount = 25,
        FallenCount = 5,
        IsProvisional = gecici,
        ComputedAt = new DateTimeOffset(yil, ay, 15, 0, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Endeks_taban_aydan_zincirleme_hesaplaniyor_en_yeni_ay_once()
    {
        var endeks = FiyatEndeksiServisi.Olustur([
            Satir(2026, 10, "protein-tozu", 1.05m),
            Satir(2026, 11, "protein-tozu", 0.98m, gecici: true),
        ]);

        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1)], endeks.Aylar.Select(a => a.Ay));
        Assert.Equal(105.0m, endeks.Aylar[1].Kategoriler[0].Endeks);
        // 100 × 1,05 × 0,98 = 102,9
        Assert.Equal(102.9m, endeks.Aylar[0].Kategoriler[0].Endeks);
        Assert.Equal(-2.00m, endeks.Aylar[0].Kategoriler[0].AylikDegisimYuzde);
        Assert.True(endeks.Aylar[0].Gecici);
        Assert.False(endeks.Aylar[1].Gecici);
    }

    // Bir ay eşiği geçemeyen kategoride zincir kopuyor: o aydan sonraki endeks uydurulmuyor, aylık değişim gösteriliyor.
    [Fact]
    public void Arada_eksik_ay_olan_kategoride_endeks_yok_aylik_degisim_var()
    {
        var endeks = FiyatEndeksiServisi.Olustur([
            Satir(2026, 10, PriceIndexMonth.AllCategories, 1.01m),
            Satir(2026, 11, PriceIndexMonth.AllCategories, 1.02m),
            Satir(2026, 11, "kreatin", 1.03m),
        ]);

        var kasim = endeks.Aylar[0].Kategoriler;
        Assert.Equal(PriceIndexMonth.AllCategories, kasim[0].Kategori); // bütün kategoriler satırı önce
        Assert.Equal(103.0m, kasim[0].Endeks); // 100 × 1,01 × 1,02 = 103,02
        var kreatin = Assert.Single(kasim, k => k.Kategori == "kreatin");
        Assert.Null(kreatin.Endeks);
        Assert.Equal(3.00m, kreatin.AylikDegisimYuzde);
    }

    // Türkiye UTC+3: 30 Eylül 21:30 UTC, Türkiye'de 1 Ekim.
    [Fact]
    public void Ay_turkiye_saatiyle_basliyor()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), FiyatEndeksiServisi.AyBasi(new DateTimeOffset(2026, 9, 30, 21, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 9, 1), FiyatEndeksiServisi.AyBasi(new DateTimeOffset(2026, 9, 30, 20, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Csv_noktali_ondalik_ve_eskiden_yeniye()
    {
        var csv = FiyatEndeksiServisi.Csv(FiyatEndeksiServisi.Olustur([
            Satir(2026, 10, "protein-tozu", 1.0141m),
            Satir(2026, 11, "protein-tozu", 1.02m, gecici: true),
        ])).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("ay,kategori,endeks_eylul_2026_100,aylik_degisim_yuzde", csv[0]);
        Assert.Equal("2026-10,protein-tozu,101.4,1.41,100,10,25.0,5.0,hayir", csv[1]);
        // 100 × 1,0141 × 1,02 = 103,44
        Assert.Equal("2026-11,protein-tozu,103.4,2.00,100,10,25.0,5.0,evet", csv[2]);
    }
}

/// <summary>
/// Kendi veritabanı (TEST_DB'nin adı + "_endeks"): UrunListesiVeritabani TEST_DB'yi kendisi silip kuruyor, iki fikstür
/// aynı veritabanında çakışırdı. Tarihler sabit (Eylül-Ekim 2026), "şimdi"ye bağlı değil.
/// </summary>
public sealed class FiyatEndeksiVeritabani : IAsyncLifetime
{
    public string? Baglanti { get; } = Turet(Environment.GetEnvironmentVariable("TEST_DB"));

    private static string? Turet(string? testDb)
    {
        if (string.IsNullOrEmpty(testDb))
            return null;
        var builder = new NpgsqlConnectionStringBuilder(testDb);
        builder.Database += "_endeks";
        return builder.ConnectionString;
    }

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

        var markalar = Enumerable.Range(1, 5).Select(i => new Brand { Name = $"Endeks Markası {i}", BaseUrl = $"https://e{i}.example" }).ToList();
        db.Brands.AddRange(markalar);
        var n = 0;
        // Fiyatlar her ayın 5'inden başlayarak günde bir, 12:00 UTC (Türkiye'de 15:00, ay sınırından uzak).
        void Ekle(Brand marka, string? kategori, decimal[] eylul, decimal[] ekim)
        {
            n++;
            var urun = new Product { Brand = marka, Name = $"Endeks Ürünü {n}", Url = $"https://endeks.example/{n}", Category = kategori };
            for (var i = 0; i < eylul.Length; i++)
                urun.PriceHistories.Add(new PriceHistory { Price = eylul[i], ScrapedAt = new DateTimeOffset(2026, 9, 5 + i, 12, 0, 0, TimeSpan.Zero) });
            for (var i = 0; i < ekim.Length; i++)
                urun.PriceHistories.Add(new PriceHistory { Price = ekim[i], ScrapedAt = new DateTimeOffset(2026, 10, 5 + i, 12, 0, 0, TimeSpan.Zero) });
            db.Products.Add(urun);
        }
        static decimal[] On(decimal fiyat) => [.. Enumerable.Repeat(fiyat, 10)];

        // Protein tozu: beş markada 30 ürün; 15'i %10 zamlanmış, 15'i aynı.
        for (var i = 0; i < 30; i++)
            Ekle(markalar[i % 5], "protein-tozu", On(100m), On(i < 15 ? 110m : 100m));
        // Tek günlük okuma (50) ayın fiyatını çekmiyor: günlerin medyanı 100. Bu ürün yine "aynı" sayılmalı.
        Ekle(markalar[0], "protein-tozu", [.. On(100m), 50m], On(100m));
        // Dört kat: dışarıda bırakılıyor ve sayılıyor.
        Ekle(markalar[1], "protein-tozu", On(100m), On(400m));
        // Ekim'de yalnız beş gün görülmüş: eşleşmiyor.
        Ekle(markalar[2], "protein-tozu", On(100m), [100m, 100m, 100m, 100m, 100m]);
        // Kategorisiz: hiçbir satıra girmiyor.
        Ekle(markalar[3], null, On(100m), On(200m));
        // Kreatin: iki markada 10 ürün, aynı fiyat. Eşiğin (30 ürün) altında, kendi satırı yok; "tum"a giriyor.
        for (var i = 0; i < 10; i++)
            Ekle(markalar[i % 2], "kreatin", On(50m), On(50m));

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class FiyatEndeksiVeritabaniTests(FiyatEndeksiVeritabani veri) : IClassFixture<FiyatEndeksiVeritabani>
{
    [VeritabaniFact]
    public async Task Ay_icinde_gecici_ay_bitince_kesin_ikinci_cagrida_degismiyor()
    {
        await using var db = veri.Baglam();
        var servis = new FiyatEndeksiServisi(db, NullLogger<FiyatEndeksiServisi>.Instance);

        // 15 Ekim: Ekim geçici.
        await servis.TazeleAsync(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var ara = await db.PriceIndexMonths.AsNoTracking().ToListAsync();
        Assert.All(ara, s => Assert.True(s.IsProvisional));
        Assert.Equal(new[] { "protein-tozu", PriceIndexMonth.AllCategories }, ara.Select(s => s.Category).Order());

        // 2 Kasım: Ekim kesinleşiyor, Kasım'da veri yok.
        await servis.TazeleAsync(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        await servis.TazeleAsync(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        var satirlar = await db.PriceIndexMonths.AsNoTracking().ToDictionaryAsync(s => s.Category);
        Assert.Equal(2, satirlar.Count);
        Assert.All(satirlar.Values, s => Assert.False(s.IsProvisional));

        // Protein tozu: 31 eşleşen ürün (30 + tek günlük okumalı), 15'i ×1,1 → √(1,1^15 … ) = 1,1^(15/31).
        var protein = satirlar["protein-tozu"];
        Assert.Equal((31, 5, 15, 0, 1), (protein.ProductCount, protein.BrandCount, protein.RisenCount, protein.FallenCount, protein.ExcludedCount));
        Assert.Equal((double)Math.Pow(1.1, 15.0 / 31), (double)protein.Ratio, 6);

        // Bütün kategoriler: + 10 kreatin (aynı fiyat) = 41 ürün; kategorisiz ürün yok.
        var tum = satirlar[PriceIndexMonth.AllCategories];
        Assert.Equal((41, 5, 1), (tum.ProductCount, tum.BrandCount, tum.ExcludedCount));
        Assert.Equal(Math.Pow(1.1, 15.0 / 41), (double)tum.Ratio, 6);

        var endeks = await servis.OkuAsync();
        var ekim = Assert.Single(endeks.Aylar);
        Assert.Equal(Math.Round(100m * protein.Ratio, 1), ekim.Kategoriler.Single(k => k.Kategori == "protein-tozu").Endeks);
    }
}

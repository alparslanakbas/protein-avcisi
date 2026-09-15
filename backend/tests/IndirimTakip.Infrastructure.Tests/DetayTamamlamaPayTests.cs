using IndirimTakip.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;
using static IndirimTakip.Infrastructure.Scraping.ProductDetailBackfillService;

namespace IndirimTakip.Infrastructure.Tests;

// Detay tamamlama kotasının dağıtımı. Sabit eşit payda (150 / 13 = 12) işi
// bitmiş markaların payı boşa gidiyor, West'in 161 bekleyen ürünü günde 12
// ile ilerliyordu.
public class DetayTamamlamaPayTests
{
    private static MarkaIhtiyaci M(string ad, int hic, int yeniden = 0) => new(ad, hic, yeniden);

    // 15 Eylül canlı sayıları.
    private static readonly MarkaIhtiyaci[] Canli =
    [
        M("West Nutrition", 161), M("ProteinOcean", 121, 1), M("Nois Nutrition", 33),
        M("Kiperin", 20, 38), M("SSN", 2, 1), M("Torq Nutrition", 1, 156), M("Grizzone", 1, 80),
        M("Fellas", 0, 118), M("Muscle Pump", 0, 79), M("Swiss Nutrition", 0, 55),
        M("GNC", 0, 51), M("BigJoy", 0, 38), M("Hardline", 0, 34),
    ];

    [Fact]
    public void KotaAsilmiyorVeTamamenKullaniliyor()
    {
        var paylar = PayDagit(Canli, 150);
        Assert.Equal(150, paylar.Values.Sum());
    }

    // Asıl hedef: hiç bakılmamış ürünler yeniden kontrolden ÖNCE geliyor.
    // Bekleyen iş 150'yi aşıyorsa yeniden kontrole tek pay gitmez.
    [Fact]
    public void HicBakilmamisUrunlerOnce()
    {
        var paylar = PayDagit(Canli, 150);

        Assert.Equal(0, paylar["Fellas"]);
        Assert.Equal(0, paylar["Hardline"]);
        Assert.Equal(33, paylar["Nois Nutrition"]);
        Assert.Equal(20, paylar["Kiperin"]);
        Assert.Equal(1, paylar["Torq Nutrition"]);
        // Büyük iki marka kalanı eşit paylaşıyor (±1).
        Assert.InRange(paylar["West Nutrition"], 46, 47);
        Assert.InRange(paylar["ProteinOcean"], 46, 47);
    }

    // Tek markanın bütün kotayı alıp VM'i ve marka sitesini yormaması için.
    [Fact]
    public void MarkaTavaniUygulaniyor()
    {
        var paylar = PayDagit([M("West Nutrition", 500), M("Nois Nutrition", 5)], 150);

        Assert.Equal(YeniUrunMarkaTavani, paylar["West Nutrition"]);
        Assert.Equal(5, paylar["Nois Nutrition"]);
    }

    // Yeni iş azsa artan kota yeniden kontrole gidiyor, ama eski eşit pay
    // kadar: tazeleme tek markada birikmesin.
    [Fact]
    public void ArtanKotaYenidenKontroleTavanlaGidiyor()
    {
        var paylar = PayDagit([M("West Nutrition", 10), M("Torq Nutrition", 0, 156), M("GNC", 0, 5)], 150);

        Assert.Equal(10, paylar["West Nutrition"]);
        Assert.Equal(YenidenKontrolMarkaTavani, paylar["Torq Nutrition"]);
        Assert.Equal(5, paylar["GNC"]);
    }

    [Fact]
    public void BekleyenIsYoksaPaySifir()
    {
        var paylar = PayDagit([M("Hardline", 0), M("GNC", 0)], 150);
        Assert.All(paylar.Values, p => Assert.Equal(0, p));
    }

    // Kota marka sayısından azsa (1 ürün / 3 marka) tamsayı bölme 0 verip
    // kimseye pay düşmemesine yol açmamalı.
    [Fact]
    public void KucukKotaDaDagitiliyor()
    {
        var paylar = PayDagit([M("A", 5), M("B", 5), M("C", 5)], 2);
        Assert.Equal(2, paylar.Values.Sum());
    }

    // Gruplu koşullu sayım EF tarafından SQL'e çevrilebilmeli; çevrilemezse
    // hata derlemede değil canlıda, turun başında çıkar. Bağlantı açılmıyor,
    // yalnızca sorgu metni üretiliyor.
    [Fact]
    public void IhtiyacSorgusuSqlCevriliyor()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=yok")
            .Options;
        using var db = new AppDbContext(options);

        // Çeviri başarısızsa ToQueryString istisna atar; testin asıl kanıtı bu.
        var sql = IhtiyacSorgusu(db, ["West Nutrition", "Nois Nutrition"]).ToQueryString();

        // Üretilen SQL canlı veritabanında elle çalıştırılıp sayılar
        // karşılaştırılabilsin diye isteğe bağlı olarak dosyaya yazılıyor.
        var hedef = Environment.GetEnvironmentVariable("IHTIYAC_SQL_DOSYASI");
        if (!string.IsNullOrEmpty(hedef))
            File.WriteAllText(hedef, sql);

        // Sayımlar veritabanında gruplanmalı; istemci tarafına düşen bir
        // sorgu bütün ürün satırlarını çekerdi.
        Assert.Contains("GROUP BY", sql);
    }
}

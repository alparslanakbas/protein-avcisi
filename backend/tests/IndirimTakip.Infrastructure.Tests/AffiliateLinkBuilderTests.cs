using IndirimTakip.Infrastructure.Deals;

namespace IndirimTakip.Infrastructure.Tests;

public class AffiliateLinkBuilderTests
{
    // Gerçek takip kodu değil, test için uydurulmuş bir değer.
    private const string Code = "abc123test";

    private static AffiliateOptions Options() => new()
    {
        TrackingCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Hardline"] = Code,
        },
    };

    [Fact]
    public void ProgramiOlanMarkadaTakipKoduEkleniyor()
    {
        // Markanın kendi aracının ürettiği biçimin birebir aynısı:
        // https://www.hardlinenutrition.com/meet-fit-paketi?tracking=...
        var url = AffiliateLinkBuilder.Apply(
            "https://www.hardlinenutrition.com/meet-fit-paketi", "Hardline", Options());

        Assert.Equal($"https://www.hardlinenutrition.com/meet-fit-paketi?tracking={Code}", url);
    }

    [Fact]
    public void ProgramiOlmayanMarkadaAdresDegismiyor()
    {
        const string original = "https://takehiq.com/products/creatine";
        Assert.Equal(original, AffiliateLinkBuilder.Apply(original, "HIQ", Options()));
    }

    [Fact]
    public void MarkaAdiBuyukKucukHarfeDuyarsizEslesiyor()
    {
        var url = AffiliateLinkBuilder.Apply("https://x.com/a", "hardline", Options());
        Assert.Contains($"tracking={Code}", url);
    }

    [Fact]
    public void AdresteZatenSorguVarsaAmpersandKullaniliyor()
    {
        // Yanlış ayraç linki tamamen bozardı.
        var url = AffiliateLinkBuilder.Apply(
            "https://www.hardlinenutrition.com/urun?renk=mavi", "Hardline", Options());

        Assert.Equal($"https://www.hardlinenutrition.com/urun?renk=mavi&tracking={Code}", url);
    }

    [Fact]
    public void BaglantiCapasiVarsaParametreOndanOnceGeliyor()
    {
        // Çapadan sonra eklenirse sunucu parametreyi hiç görmez.
        var url = AffiliateLinkBuilder.Apply(
            "https://www.hardlinenutrition.com/urun#detay", "Hardline", Options());

        Assert.Equal($"https://www.hardlinenutrition.com/urun?tracking={Code}#detay", url);
    }

    [Fact]
    public void AyniParametreZatenVarsaIkinciKezEklenmiyor()
    {
        const string original = "https://www.hardlinenutrition.com/urun?tracking=baskabiri";
        Assert.Equal(original, AffiliateLinkBuilder.Apply(original, "Hardline", Options()));
    }

    [Fact]
    public void KodBosSaBirakiliyor()
    {
        var options = new AffiliateOptions
        {
            TrackingCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Hardline"] = "  " },
        };
        const string original = "https://www.hardlinenutrition.com/urun";
        Assert.Equal(original, AffiliateLinkBuilder.Apply(original, "Hardline", options));
    }

    [Fact]
    public void MarkaAdiYoksaAdresDegismiyor()
    {
        const string original = "https://www.hardlinenutrition.com/urun";
        Assert.Equal(original, AffiliateLinkBuilder.Apply(original, null, Options()));
    }
}

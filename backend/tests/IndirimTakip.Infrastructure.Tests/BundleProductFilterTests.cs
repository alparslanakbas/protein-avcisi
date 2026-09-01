using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Adların tamamı gerçek kataloglardan (Provitamin, GNC, Supra Protein).
public class BundleProductFilterTests
{
    [Theory]
    // GNC
    [InlineData("Spor Rutini Paketi")]
    [InlineData("Kas Desteği Paketi")]
    [InlineData("Cilt Sağlığı Paketi")]
    // Supra Protein — "seti" biçimi, 2 Eylül'de eklendi
    [InlineData("4’lü Deneme Seti – Kolajen & C Vitamini")]
    [InlineData("Sporcu Paketi")]
    [InlineData("Eklem Sağlığı Paketi")]
    // Provitamin — tamamı büyük harf yazım
    [InlineData("FITNESS PAKETİ")]
    [InlineData("FITNESS PAKETİ - MEGA")]
    public void CokUrunluSetleriTanir(string ad)
    {
        Assert.True(BundleProductFilter.IsBundle(ad));
    }

    [Theory]
    [InlineData("WHEY Protein Isolate - Çikolata Aromalı")]
    [InlineData("Creatine MonoHydrate – 510 g (100 servis)")]
    [InlineData("Marine Collagen (Tablet Form)")]
    [InlineData("Multi Magnezyum Complex")]
    [InlineData("Vitamin B12 1000 MCG - 100 Tablet")]
    public void GercekUrunleriEtkilemez(string ad)
    {
        Assert.False(BundleProductFilter.IsBundle(ad));
    }

    // Türkçe harf tuzağı: "PAKETİ" içindeki noktalı İ, OrdinalIgnoreCase ile
    // "i"ye katlanmıyor — normalize edilmezse tamamı büyük harfli adlar kaçar.
    [Theory]
    [InlineData("KREATİN PAKETİ")]
    [InlineData("Kreatin Paketi")]
    [InlineData("kreatin paketi")]
    [InlineData("DENEME SETİ")]
    public void BuyukKucukHarf_ve_noktali_I_farkindan_etkilenmez(string ad)
    {
        Assert.True(BundleProductFilter.IsBundle(ad));
    }

    // Kalıp DAR: "set"/"paket" gövdesi kelimenin kendisi olmalı, rastgele
    // bir alt dize değil. Katalog tarandı (2 Eylül) — bu gövdeler mevcut
    // 2009 ürünün hiçbirinde geçmiyor, yani eklenmesi bir şeyi taşımadı.
    [Theory]
    [InlineData("Reset Formula")]
    [InlineData("Beta Alanin Preset")]
    [InlineData("Korseti")]
    public void Baska_kelimenin_icindeki_set_yakalanmaz(string ad)
    {
        Assert.False(BundleProductFilter.IsBundle(ad));
    }

    // Katalogdaki gerçek ürün: TEK servislik bir preworkout, set DEĞİL.
    // Kalıp çıplak "paket" arasaydı bu ürün sessizce elenirdi.
    [Fact]
    public void Tek_paket_servis_urunu_set_sayilmaz()
    {
        Assert.False(BundleProductFilter.IsBundle(
            "Buster Preworkout L-Arjinin & Beta- Alanin 22.4 gram Tek Paket Servis"));
    }
}

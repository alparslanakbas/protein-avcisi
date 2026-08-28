using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Örnekler Torq'un canlı kataloğundan alındı (2026-08-28): eklendiğinde
// 52 ürün kategorisizdi, bu kelimeler onların bir kısmını karşılıyor.
public class TorqCategoryTests
{
    [Theory]
    [InlineData("%100 Yer Fıstığı Ezmesi - Creamy  350 Gr", "saglikli-atistirmaliklar")]
    [InlineData("8'li Paket Pan Multigrano Ekmek", "saglikli-atistirmaliklar")]
    [InlineData("Ciabatta Rustica Ekmek", "saglikli-atistirmaliklar")]
    [InlineData("Muscle Rice Mikronize Pirinç Unu Vanilya 1000 GR", "kilo-hacim")]
    [InlineData("D3K2 (1000 IU / 11,25 mcg)  60 Yumuşak Kapsül", "vitamin")]
    public void KategorisizUrunlerArtikEslesiyor(string name, string beklenen)
    {
        Assert.Equal(beklenen, ProductAttributeParser.InferCategory(name));
    }

    [Theory]
    [InlineData("Torq Athletics Loop Band Direnç Bandı - Orta Pembe")]
    [InlineData("Torq Athletics PN-X Ağırlık Kemeri - Siyah L/XL")]
    [InlineData("Torq Athletics Pro Wrist Wraps  Siyah")]
    [InlineData("Torq Athletics Big Grip Pro Lifting Strap Siyah")]
    [InlineData("Fitness Eldiveni  Siyah  XL Beden")]
    [InlineData("Hap Kutusu")]
    public void EkipmanUrunleriElenıyor(string name)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(name));
    }

    [Theory]
    [InlineData("Whey Protein Tozu 1000 Gr")]
    [InlineData("Creatine Monohydrate 300 Gr")]
    [InlineData("Protein Bar Çikolata")]
    public void GercekTakviyelerElenmıyor(string name)
    {
        Assert.False(NonSupplementProductFilter.IsAccessoryOrApparel(name));
    }
}

// Yeşilmarka karma katalog: kozmetik bakım/seyahat setlerini spor
// kategorisine koymuş, süzgeç bunları yakalamalı — ama "seti" tek kelime
// olarak kullanılamaz, gerçek takviye adlarının içinde geçiyor.
public class KozmetikSetFiltresiTests
{
    [Theory]
    [InlineData("Kokusuz Bakım Seti (İhrama Uygun Hac ve Umre Seyahat Seti)")]
    [InlineData("Seyahat Seti")]
    public void KozmetikSetleriEleniyor(string name)
        => Assert.True(IndirimTakip.Infrastructure.Scraping.NonSupplementProductFilter.IsAccessoryOrApparel(name));

    [Theory]
    [InlineData("Kuersetin Karamuk Ekstresi (Berberin) Çinko 30 Kapsül")]
    [InlineData("Whey Protein Tozu - Aromasız")]
    public void GercekTakviyelerElenmiyor(string name)
        => Assert.False(IndirimTakip.Infrastructure.Scraping.NonSupplementProductFilter.IsAccessoryOrApparel(name));
}

using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Süzgeç, kaçan ürünler bulundukça genişliyor. Test yazmanın asıl sebebi
// genişlemenin YAN ETKİSİNİ yakalamak: eklenen bir kelime, gerçek bir
// takviyeyi yanlışlıkla eleyebilir.
public class NonSupplementProductFilterTests
{
    [Theory]
    // Giyim / ekipman / aksesuar
    [InlineData("Commander Gold T-Shirt")]
    [InlineData("Commander 700ml Shaker")]
    [InlineData("Commander Havlu")]
    [InlineData("HIQ Hoodie Siyah")]
    [InlineData("Ağırlık Kemeri L")]
    // Gıda / çeşni — 31 Ağustos'ta eklendi (Commander Nutrition katalogu)
    [InlineData("Fit Grains İthal Basmati Pirinç (1000g)")]
    [InlineData("Seed'n Grains Pembe Himalaya Tuzu (250g)")]
    [InlineData("Dr. Pan Bal Aromalı Hardal Şekersiz (260g)")]
    [InlineData("Dr. Pan Sriracha Sos Şekersiz (260g)")]
    [InlineData("Dr.Pan Sweet Drops Lemon Cheesecake (30ml)")]
    public void Takviye_olmayanlari_eliyor(string productName)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(productName));
    }

    [Theory]
    // REGRESYON: "pirinç"/"rice" süzgece EKLENMEMELİ — Cream of Rice gerçek
    // bir sporcu gıdası ve aynı katalogda satılıyor. Genel kelime eklemek
    // bu ürünleri sessizce siteden düşürürdü.
    [InlineData("Dr. Pan Rice Cream Çilekli (400g)")]
    [InlineData("Dr. Pan Oat Cream (400g)")]
    [InlineData("HIQ Cream of Rice 1000g")]
    // Gerçek takviyeler dokunulmadan geçmeli
    [InlineData("Gold Whey Protein 900g (30 Servis)")]
    [InlineData("Creatine Monohydrate Micronized")]
    [InlineData("Overthrow Pre-Workout 375g (25 Servis)")]
    [InlineData("Reload BCAA+ 200g (20 Servis)")]
    [InlineData("Fitnut %100 Badem Ezmesi (Net 250g)")]
    // "performans" bilinçli olarak süzgeçte yok: gerçek takviye paketleri de
    // bu kelimeyi taşıyor.
    [InlineData("Orta Güç Performans Paketi")]
    public void Gercek_takviyeleri_elemiyor(string productName)
    {
        Assert.False(NonSupplementProductFilter.IsAccessoryOrApparel(productName));
    }

    [Theory]
    // REGRESYON: liste "havlu" ve "çanta" içeriyordu ama bu iki ürün canlıya
    // GİRDİ, çünkü Türkçe ek kelime sınırını kaydırıyor: `havlu` kalıbı
    // "havlusu" ile eşleşmiyor.
    [InlineData("Just Profesyonel Antrenman Havlusu (Smart)")]
    [InlineData("Just Leather Sport Bag -Şık Suni Deri Spor Çantası (Kahverengi & Siyah)")]
    [InlineData("Siyah Havlular")]
    [InlineData("Spor Çantaları")]
    [InlineData("Protein Shakerı")]
    public void TurkceEkAlanAksesuarlarDaElenir(string ad)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }

    [Theory]
    // Gerçek takviyeler etkilenmemeli.
    [InlineData("Whey Protein Tozu 2000 Gr")]
    [InlineData("Creatine Monohydrate 300 Gr")]
    [InlineData("Cream of Rice 1000 Gr")]
    public void GercekTakviyelerElenmiyor(string ad)
    {
        Assert.False(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }
}

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

    [Theory]
    // REGRESYON: bu altı ürün CANLIYA GİRDİ, kullanıcı ekran görüntüsüyle
    // bildirdi. Hepsi listedeki bir kelimenin farklı yazımı/ekli hâliydi.
    [InlineData("Just Likralı Antrenman Atleti")]
    [InlineData("Just 8 Loop Strap")]
    [InlineData("Protein 7 Pill Box -Tablet Saklama Kabı Aksesuar Protein7 Diğer")]
    [InlineData("Protein 7 Powder Box -Toz Saklama Kabı Aksesuar Protein7 Diğer")]
    [InlineData("Xpro Pill Box -Tablet Saklama Kabı Aksesuar Xpro Nutrition")]
    [InlineData("Antrenman Havlusu 50x90 cm")]
    public void CanliyaKacanAksesuarlarArtikElenir(string ad)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }

    [Theory]
    // YANLIŞ POZİTİF KORUMASI: "Kutu"/"Box" meşru çoklu paketlerde geçiyor,
    // kör silme bu ürünleri de götürürdü.
    [InlineData("Fındıklı Protein Bar 16lı Kutu x 50 gram")]
    [InlineData("SSN Command Quadro Whey 22 Gr x 40 Şase Kutu 880 Gr")]
    [InlineData("SWISS WHEY GOLD DELUXE SERIES SAŞE 24 ADET - 1 Kutu / 24 Servis")]
    [InlineData("PROTEİN BAR KARMA KUTU")]
    // "atletik" bir aksesuar değil; ek desteği bunu yakalamamalı.
    [InlineData("Atletik Performans Kompleksi 90 Kapsül")]
    // "canta[a-z]*" kalıbı "CANTAloupe"u yakalıyordu — katalogdaki gerçek
    // kurban. Bir whey proteini spor çantası sanıp sessizce elerdi; Nois
    // scraper'ı bu yüzden ortak süzgeci hiç kullanmıyordu.
    [InlineData("Nois Whey Rex 900G Protein Tozu - Cantaloupe")]
    [InlineData("BCAA Cantaloupe Aromalı")]
    [InlineData("Cantaloupe")]
    public void MesruUrunlerElenmiyor(string ad)
    {
        Assert.False(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }

    // Cantaloupe düzeltmesi gerçek çantaları kaçırmamalı — ek listesi
    // daraltıldı ama kapsam korundu.
    [Theory]
    [InlineData("Spor Çantası")]
    [InlineData("Hardline Spor Cantasi")]
    [InlineData("Gym Canta")]
    [InlineData("Antrenman Cantalari")]
    public void GercekCantalarHalaEleniyor(string ad)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }

    // HEDİYE SHAKER: aksesuar hediyeli TAKVİYE, aksesuar değil. Adlar gerçek
    // kataloglardan (Imperium 7.200 TL'lik set, HIQ başlangıç paketleri).
    [Theory]
    [InlineData("Kilo Aldırıcı Ultra Set - Shaker Hediyeli")]
    [InlineData("HIQ Fitness Başlangıç Paketi + Shaker")]
    [InlineData("HIQ Amino Başlangıç Paketi + Shaker")]
    [InlineData("HIQ Enerji Başlangıç Paketi + Shaker")]
    public void ShakerHediyeliTakviyePaketiElenmiyor(string ad)
    {
        Assert.False(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }

    // İstisna DAR olmalı: shaker'ın KENDİSİ ürünse hâlâ eleniyor.
    [Theory]
    [InlineData("Renkli Yüksek Kalite Shaker 550cc")]
    [InlineData("Space Shaker")]
    [InlineData("Prime Nutrition Shaker 500 ml.")]
    [InlineData("Batman Shaker")]
    public void GercekShakerHalaEleniyor(string ad)
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel(ad));
    }

    // İstisna YALNIZCA shaker için: hediyeli de olsa çanta çantadır.
    [Fact]
    public void HediyeliCantaYineDeEleniyor()
    {
        Assert.True(NonSupplementProductFilter.IsAccessoryOrApparel("Protein Paketi - Spor Çantası Hediyeli"));
    }
}

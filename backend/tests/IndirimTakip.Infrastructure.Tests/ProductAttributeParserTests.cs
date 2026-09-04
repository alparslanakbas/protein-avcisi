using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

public class ProductAttributeParserTests
{
    // Regresyon testi: tr-TR kültürüyle küçük harfe çevirme, büyük "I"yi
    // noktasız "ı" yapıp İngilizce anahtar kelimelerle eşleşmeyi
    // engelliyordu (bkz. CLAUDE.md, 2026-08-14 "kreatin araması" bug'ı).
    [Theory]
    [InlineData("CREATINE", "kreatin")]
    [InlineData("CREATINE CHEWS", "kreatin")]
    [InlineData("HIQ Creatine 240g", "kreatin")]
    [InlineData("SSN ... Whey Protein Tozu", "protein-tozu")]
    [InlineData("HIQ Beta Alanine 300g", "amino-asitler")]
    [InlineData("HIQ B.M.F. Extreme Thermo Burner", "yag-yakici")]
    // Regresyon testi: l-carnitine-cla CategoryKeywords'te hiç yoktu, bu
    // yüzden HIQ/Hardline/ProteinOcean'ın L-Carnitine ürünleri yanlışlıkla
    // yag-yakici'nin anahtar kelime listesine (l-carnitine/karnitin/cla)
    // takılıp o kategoriye düşüyordu — kullanıcı canlıda kategori sayfasında
    // L-Carnitine'de sadece SSN görünce fark etti (SSN kendi slug'ını elle
    // veriyor, diğerleri InferCategory'ye bağımlı).
    [InlineData("HIQ L-Carnitine Tartare 120 Kapsül", "l-carnitine-cla")]
    [InlineData("Hardline L-Karnitin Matrix 3000 Mg", "l-carnitine-cla")]
    [InlineData("L-CARNITINE SHOT", "l-carnitine-cla")]
    // Regresyon testi: Türkçe büyük noktalı "İ" ToLowerInvariant ile hiç
    // küçülmüyordu ("VİTAMİNİ" -> "vİtamİnİ"), bu da tüm-büyük-harfli Türkçe
    // ürün isimlerinin "vitamin" gibi anahtar kelimelerle eşleşmesini
    // engelliyordu.
    [InlineData("C VİTAMİNİ EFERVESAN", "vitamin")]
    // Kapsamlı kategori taraması (2026-08-17): önceden hiçbir kategoriye
    // düşmeyen gerçek ürün örnekleri, canlı veriden bulundu.
    [InlineData("HIQ Gain Deluxe 3 kg", "kilo-hacim")]
    [InlineData("HIQ Maltodextrin 1500g Unflavored", "kilo-hacim")]
    [InlineData("GLYCINE", "amino-asitler")]
    [InlineData("Taurine 300 Gr", "amino-asitler")]
    [InlineData("BIOTIN", "vitamin")]
    [InlineData("HIQ Collagen Vitaplus 300g", "protein-tozu")]
    [InlineData("Hipro 908 Gr", "protein-tozu")]
    // Markalı/özel karışım isimleri (isimden çıkarım değil tahmin olurdu)
    // bilinçli olarak null kalmalı — ör. GH-UP, Smash Pro gibi.
    [InlineData("HIQ Gh-Up 120 Kapsül", null)]
    // İkinci tur (kullanıcı isteğiyle tüm 235 ürün tek tek incelendi):
    // markalı ama şeffaf isimler (bileşeni doğrudan taşıyanlar) eklendi.
    [InlineData("Hardline Creapure 120 Kapsül", "kreatin")]
    [InlineData("Hardline Glutapure 300 Gr", "amino-asitler")]
    [InlineData("HIQ High Pro+ 900gr", "protein-tozu")]
    [InlineData("ALCAR", "l-carnitine-cla")]
    [InlineData("Hardline Carnıfıt 500 Ml", "l-carnitine-cla")]
    [InlineData("CAFFEINE", "pre-workout")]
    [InlineData("TERMOJENİK PAKET", "yag-yakici")]
    [InlineData("HIQ Glucoflex 60 Kapsül", "vitamin")]
    [InlineData("HIQ Curcumin 30 Sıvı Kapsül", "vitamin")]
    [InlineData("SPIRULINA POWDER", "vitamin")]
    // Gliserol: canlıda 13 ürünün 7'si hiçbir kategoride değildi. Amino asit
    // DEĞİL — antrenman öncesi hiperhidrasyon/pump maddesi.
    [InlineData("Bigjoy Hydro Glycerol 140g", "pre-workout")]
    [InlineData("GLISEROL", "pre-workout")]
    [InlineData("Pure Waves Gliserol - 200 g", "pre-workout")]
    [InlineData("SWISS GLYCEROL %90 HYDROXYPUMP", "pre-workout")]
    // Sıra koruması: adında hem amino asit hem gliserol geçen bir ürün
    // amino-asitler'de kalmalı, çünkü o kategori listede önce geliyor.
    [InlineData("BCAA + Glycerol Blend", "amino-asitler")]
    [InlineData("Bilinmeyen bir ürün adı", null)]
    public void InferCategory_dogru_kategoriyi_donuyor(string productName, string? expectedCategory)
    {
        var result = ProductAttributeParser.InferCategory(productName);

        Assert.Equal(expectedCategory, result);
    }

    [Fact]
    public void InferCategory_ilk_eslesen_kategoriyi_donuyor()
    {
        // "protein" hem protein-tozu hem (dolaylı olarak) başka kategorilerde
        // geçebilir — CategoryKeywords sırasına göre İLK eşleşen kazanmalı.
        var result = ProductAttributeParser.InferCategory("Whey Protein Creatine Kombinasyonu");

        Assert.Equal("protein-tozu", result);
    }

    [Theory]
    [InlineData("SSN Whey 2100 Gr Protein Tozu", "2100 Gr")]
    [InlineData("HIQ Creatine 240g", "240 Gr")]
    [InlineData("Ürün 1,5 Kg Paket", "1.5 Kg")]
    [InlineData("Boyut bilgisi olmayan ürün", null)]
    public void ExtractSize_dogru_boyutu_cikariyor(string productName, string? expectedSize)
    {
        var result = ProductAttributeParser.ExtractSize(productName);

        Assert.Equal(expectedSize, result);
    }

    [Theory]
    [InlineData("SSN Whey 2100 Gr (Çikolata) Protein Tozu", "Çikolata")]
    [InlineData("Ürün (60 Ml *18 Adet)", null)] // boyut bilgisi taşıyan parantez aroma sayılmamalı
    [InlineData("Aroma parantezi olmayan ürün", null)]
    public void ExtractFlavor_boyut_parantezini_aroma_sanmiyor(string productName, string? expectedFlavor)
    {
        var result = ProductAttributeParser.ExtractFlavor(productName);

        Assert.Equal(expectedFlavor, result);
    }

    // Aşağıdaki vakaların TAMAMI canlı veriden alındı. Alan kullanıcıya
    // "Aroma: ..." olarak gösteriliyor ve aramaya dahil, bu yüzden yanlış
    // değer boş değerden kötü.
    [Theory]
    // Tire biçimi — Yeşilmarka'nın mağaza API'si aromayı ismin sonuna koyuyor.
    [InlineData("BCAA 4:1:1 - Ananas", "Ananas")]
    [InlineData("Whey Protein Tozu - Anamur Muzu", "Anamur Muzu")]
    [InlineData("Whey Protein Tozu - Beyoğlu Çikolatası", "Beyoğlu Çikolatası")]
    // Ünsüz yumuşaması: "çileği" gövdesi "çilek" ile başlamıyor.
    [InlineData("Whey Protein Tozu - Ereğli Çileği", "Ereğli Çileği")]
    // Noktasız ı: "AROMASIZ" invariant küçültmede "aromasiz" oluyor.
    [InlineData("Whey Protein Tozu - Aromasız", "Aromasız")]
    [InlineData("Whey Protein Tozu - Kakao/Vanilya", "Kakao/Vanilya")]
    // Büyük noktalı İ invariant kültürde hiç küçülmüyor.
    [InlineData("Ürün (ÇİLEK)", "ÇİLEK")]
    // Markalar İngilizce yazım da kullanıyor.
    [InlineData("Ürün (Creme Caramel)", "Creme Caramel")]
    public void ExtractFlavor_gercek_aromalari_yakaliyor(string productName, string? expectedFlavor)
    {
        Assert.Equal(expectedFlavor, ProductAttributeParser.ExtractFlavor(productName));
    }

    [Theory]
    // Miktar/porsiyon bilgisi — canlıda Flavor alanına yazılmış hâlleri.
    [InlineData("Ürün (40 Servis)")]
    [InlineData("Ürün (15 x 4 Doypacks)")]
    [InlineData("Ürün (30 Saşe)")]
    [InlineData("Ürün (1000 IU / 11,25 mcg)")]
    [InlineData("Torq Ürün - 60 Servis")]
    [InlineData("Ürün - 12 Adet")]
    // Etken madde, aroma değil.
    [InlineData("Ürün (Arginine)")]
    [InlineData("Ürün (Collagen)")]
    [InlineData("Ürün (Maca)")]
    // Paket içeriği.
    [InlineData("Ürün (EAA + HellFire Pre-Workout + Citrulline)")]
    // Marka adı işareti.
    [InlineData("Ürün (Creapure®)")]
    // Aroma olmayan tire son eki.
    [InlineData("Glutamine - Bitkisel Bazlı")]
    [InlineData("Ürün - LARGE")]
    public void ExtractFlavor_aroma_olmayanlari_reddediyor(string productName)
    {
        Assert.Null(ProductAttributeParser.ExtractFlavor(productName));
    }

    [Fact]
    public void GetSearchSynonyms_kreatin_aramasi_creatine_i_de_iceriyor()
    {
        var synonyms = ProductAttributeParser.GetSearchSynonyms("kreatin");

        Assert.Contains("creatine", synonyms);
        Assert.Contains("kreatin", synonyms);
    }

    [Fact]
    public void GetSearchSynonyms_eslesmeyen_terim_icin_bos_donuyor()
    {
        var synonyms = ProductAttributeParser.GetSearchSynonyms("alakasız-terim-xyz");

        Assert.Empty(synonyms);
    }

    // Porsiyon büyüklüğü, markaların açıklama metninde serbest formda geçiyor —
    // aşağıdaki kalıpların hepsi canlı veriden alınmış gerçek yazım biçimleri.
    [Theory]
    [InlineData("Porsiyon Büyüklüğü: 30 gr olarak tüketilmesi önerilir.", 30)]
    [InlineData("Her porsiyonunda (30 g) 22,1 g protein içerir.", 30)]
    // Hardline'ın kreatin açıklamasından birebir: parantez içindeki gerçek
    // gramaj (5g) alınmalı, cümlenin başındaki "1 porsiyon"daki 1 değil.
    [InlineData("1 porsiyon (1 ölçek, 5g), bir bardak su ile karıştırılır.", 5)]
    [InlineData("Günde 1 ölçek (25 g) tüketilebilir.", 25)]
    [InlineData("Servis başına 23 g protein sağlar.", 23)]
    [InlineData("Porsiyon büyüklüğü 12,5 gram", 12.5)]
    public void ExtractServingSizeGrams_gercek_aciklama_kaliplarini_cozuyor(string description, double expected)
    {
        var result = ProductAttributeParser.ExtractServingSizeGrams(description);

        Assert.Equal((decimal)expected, result);
    }

    // Makul aralığın (1-500 g) dışındaki eşleşmeler, metinde porsiyonla
    // ilgisiz bir yerden yakalanmış demektir — canlı veride bir sos ürününde
    // 0,22 g gibi bir değer bu şekilde çıkmıştı.
    [Theory]
    [InlineData("Porsiyon başına 0,22 g tuz içerir.")]
    [InlineData("Toplam porsiyon miktarı 900 g paket içindir.")]
    public void ExtractServingSizeGrams_makul_olmayan_degerleri_eliyor(string description)
    {
        var result = ProductAttributeParser.ExtractServingSizeGrams(description);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bu üründe porsiyon bilgisi hiç geçmiyor.")]
    public void ExtractServingSizeGrams_bulamayinca_null_donuyor(string? description)
    {
        var result = ProductAttributeParser.ExtractServingSizeGrams(description);

        Assert.Null(result);
    }

    // Bayi kaynakları ürün adına markayı da yazıyor. Marka adı bir kategori
    // anahtar kelimesi içerdiğinde ("Protein-ocean") tüm ürünleri yanlış
    // kategoriye düşürüyordu — canlıda ProteinOcean'ın kreatini, omega'sı ve
    // vitamini "protein tozu" olarak kaydedilmişti.
    [Theory]
    [InlineData("Proteinocean Creatine 300gr Kreatin Monohidrat", "Proteinocean", "kreatin")]
    [InlineData("Proteinocean Omega 3 45 Kapsül Balık Yağı", "Proteinocean", "vitamin")]
    [InlineData("Proteinocean Vitamin D3 60 Kapsül", "Proteinocean", "vitamin")]
    public void InferCategory_marka_adini_kategori_sanmiyor(string productName, string brandName, string expected)
    {
        Assert.Equal(expected, ProductAttributeParser.InferCategory(productName, brandName));
    }

    [Theory]
    // Marka çıkarılınca gerçek protein tozları YİNE protein tozu kalmalı.
    [InlineData("Proteinocean Whey Protein Tozu 1000gr", "Proteinocean", "protein-tozu")]
    [InlineData("BigJoy Big Whey Classic 2288gr", "BigJoy", "protein-tozu")]
    // Marka bilinmiyorsa eski davranış korunuyor.
    [InlineData("Whey Protein Tozu 1000gr", null, "protein-tozu")]
    public void InferCategory_marka_cikarilinca_dogru_kategoriyi_koruyor(string productName, string? brandName, string expected)
    {
        Assert.Equal(expected, ProductAttributeParser.InferCategory(productName, brandName));
    }

    // REGRESYON: ürünün BİÇİMİ içeriğinden önce gelir. Anahtar kelime listesi
    // sırayla taranıyor ve "protein-tozu" en başta olduğu için, adında
    // "protein" geçen barlar toz kategorisine düşüyordu. Canlı veride
    // ölçüldü: 11 markada 49 bar yanlış kategorideydi.
    [Theory]
    [InlineData("Multipower %50 Protein Bar 50gr 20 Adet")]
    [InlineData("HIQ High Pro Protein Bar 12*55g")]
    [InlineData("Grenade High Protein Bar 12 Adet")]
    [InlineData("FİTNUT GO PROTEIN BAR FISTIK&ÇİKOLATA 40G")]
    [InlineData("Protein Bar Fındık 50 Gr")]
    // Pre-workout barı da bardır — o da atıştırmalık.
    [InlineData("Pre-Workout Bar Fındık 50 Gr")]
    public void InferCategory_protein_barlarini_toz_sanmiyor(string productName)
    {
        Assert.Equal("saglikli-atistirmaliklar", ProductAttributeParser.InferCategory(productName));
    }

    [Theory]
    // Gerçek protein TOZLARI etkilenmemeli.
    [InlineData("HIQ Whey Protein Tozu 2000 Gr", "protein-tozu")]
    [InlineData("Hardline Whey 3 Matrix 2300 Gr", "protein-tozu")]
    // "bar" bir başka kelimenin İÇİNDE geçiyorsa bar değildir.
    [InlineData("Seedn Grains Barbekü Baharatı 80gr", null)]
    public void InferCategory_bar_kurali_yanlis_pozitif_uretmiyor(string productName, string? expected)
    {
        Assert.Equal(expected, ProductAttributeParser.InferCategory(productName));
    }

    // REGRESYON: "mg" paket birimi değil, etken madde DOZU. İlk eşleşmeyi
    // almak paket boyutunu doza çeviriyordu — canlıda 37 üründe böyleydi.
    [Theory]
    [InlineData("Herbina Magnezyum Sitrat 500 mg 120 Tablet", "120 Tablet")]
    [InlineData("Alpha Lipoic Acid - Alfa Lipoik Asit 200 mg 60 Kapsül", "60 Kapsül")]
    [InlineData("Amino Complex 8300 mg 300 gr Amino Asit Karışımı", "300 Gr")]
    [InlineData("L-Karnitin 2000 mg 1000 ml Yeşil Elma Aromalı", "1000 Ml")]
    [InlineData("Vitamin C Powder 1000 MG  200 Gr - 200 Servis", "200 Gr")]
    public void ExtractSize_dozu_paket_boyutu_sanmiyor(string productName, string expected)
    {
        Assert.Equal(expected, ProductAttributeParser.ExtractSize(productName));
    }

    [Fact]
    public void ExtractSize_baska_birim_yoksa_mg_gosterilir()
    {
        // Elde olan tek ölçü mg ise boş bırakmaktansa o gösteriliyor.
        Assert.Equal("2900 Mg", ProductAttributeParser.ExtractSize("SWISS GH MATRIX 2900MG"));
    }

    // GNC eklenirken (1 Eylül 2026) 45 ürünün 13'ü kategorisiz kaldı; üçü
    // bizde ZATEN kategorisi olan spor ürünüydü, kalanı vitamin/bitkisel.
    // Adlar GNC'nin gerçek kataloğundan.
    [Theory]
    [InlineData("GNC Pro Pre-W-Out – 339 g", "pre-workout")]
    [InlineData("GNC Pro Pre-W-Out – 339 g - Yaban Mersini ve Frambuaz Aromalı", "pre-workout")]
    [InlineData("GNC Pro Bulk 1340 – 5443 g (15 servis)", "kilo-hacim")]
    [InlineData("GNC CoQ-10 100 mg 30 Yumuşak Kapsül", "vitamin")]
    [InlineData("Quercetin – 60 Tablet", "vitamin")]
    [InlineData("Fish Oil - 60 Yumuşak Kapsül", "vitamin")]
    [InlineData("Triple Strenght Krill Oil – 30 Yumuşak Kapsül", "vitamin")]
    [InlineData("Bromelain – 60 Tablet", "vitamin")]
    [InlineData("Herbal Plus Turmeric Complex - 100 Kapsül", "vitamin")]
    [InlineData("GNC FOLAT 400 MCG 100 TABLET", "vitamin")]
    public void InferCategory_gnc_kataloguna_gore(string ad, string beklenen)
    {
        Assert.Equal(beklenen, ProductAttributeParser.InferCategory(ad));
    }

    // "bulk" eklenirken ölçüldü: o dizi 1100 ürün içinde başka yalnızca SSN'in
    // gainer'larında geçiyor. Onların kategorisi KAYNAKTAN geliyor (SSN kendi
    // slug'ını veriyor) ama parser'a düşselerdi de doğru yere giderlerdi —
    // "mass" zaten kilo-hacim'de. Bu test o gerekçeyi sabitliyor: ekleme
    // hiçbir ürünü protein-tozu'ndan çekip almıyor.
    [Theory]
    [InlineData("SSN Bulk Power Gainzilla Mass 5000 g (Çikolata)")]
    [InlineData("SSN Bulk Power Gainzilla Mass 1500 g")]
    public void InferCategory_bulk_gainerlari_protein_tozuna_tasimaz(string ad)
    {
        Assert.Equal("kilo-hacim", ProductAttributeParser.InferCategory(ad));
    }

    // Sıra bağımlılığı: adında hem protein hem gainer geçen ürün protein-tozu
    // kalmalı (liste sırası bilinçli, bkz. `5e8ef43`). "bulk" eklemesi bu
    // dengeyi bozmamalı.
    [Fact]
    public void InferCategory_protein_gecen_bulk_urunu_protein_tozunda_kalir()
    {
        Assert.Equal("protein-tozu", ProductAttributeParser.InferCategory("Bulk Whey Protein 1000 g"));
    }
}

using System.Globalization;
using System.Text.Json;
using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure.Catalog;

/// <summary>Yönetim panelinde markanın etiketinden elle yazılan, porsiyon başına besin değeri.</summary>
/// <param name="DigerSatirlar">
/// Makrolar dışındaki satırlar: takviye etiketlerindeki ("Kreatin Monohidrat 5 g",
/// "Kafein 200 mg", "D3 Vitamini 25 mcg") ya da tablodaki tuz, şeker gibi satırlar.
/// </param>
public sealed record ElleBesinIstegi(
    decimal? PorsiyonGram,
    decimal? Kalori,
    decimal? ProteinGram,
    decimal? KarbonhidratGram,
    decimal? YagGram,
    decimal? LifGram,
    IReadOnlyList<ElleBesinSatiri>? DigerSatirlar = null,
    bool EtiketBoyleYaziyor = false,
    int? PaketPorsiyonSayisi = null,
    bool PorsiyonBeyanYok = false);

/// <summary>Etiketteki bir satır: ad, porsiyon başına miktar ve birimi.</summary>
public sealed record ElleBesinSatiri(string? Ad, decimal? Miktar, string? Birim);

public sealed record ElleDuzenlemeSonucu(bool Bulundu, bool Kabul, string? Sebep = null, int GuncellenenSatir = 0, string? Kod = null)
{
    public static readonly ElleDuzenlemeSonucu Yok = new(false, false);
}

/// <summary>Kontrolün sonucu: yayınlanacak tablo satırları ya da ret sebebi.</summary>
internal sealed record ElleBesinKontrolu(IReadOnlyList<(string Ad, string Deger)> Satirlar, string? RetSebebi, string? RetKodu = null)
{
    public bool Kabul => RetSebebi is null;
}

/// <summary>
/// Hiçbir çekicinin okuyamadığı ürünler için kişinin girdiği kategori ve besin değeri:
/// yalnızca görsel olarak yayınlanan ve OCR'ın okuyamadığı bir etiket, ya da adı ne
/// olduğunu söylemeyen bir ürün.
/// </summary>
/// <remarks>
/// <b>Sayfanın BÜTÜN satırlarına uygulanıyor.</b> Aynı ürün sayfasının farklı boyları
/// ayrı satır olabiliyor ve aynı etiketi taşıyor; birini düzeltip ötekileri boş
/// bırakmak yarım iş olurdu.
///
/// <b>Elle işaretleniyor.</b> Tarama kategoriyi her 6 saatte bir yeniden yazıyor ve
/// kaynak tablo gönderirse besin değerini de; bayraklar kişinin değerini koruyor
/// (bkz. ScrapeIngestionService).
///
/// <b>Yazılan değerler kontrolden geçiyor.</b> Makrolar kalori kontrolüne tabi
/// (protein 25 yerine 250 yazmak toplamı bozar ve sebebiyle reddedilir). Takviye
/// etiketlerinin kontrol edilecek kalorisi yok; orada her satır kendi başına
/// kontrol ediliyor (bkz. <see cref="Kontrol"/>).
///
/// WheyProof'taki aynı özelliğin Türkçe karşılığı. Kalori hesabı AB etiketine göre:
/// karbonhidrat lifi İÇERMİYOR ve lif gram başına 2 kcal veriyor.
/// </remarks>
public sealed class ManualProductDataService(AppDbContext db)
{
    public async Task<ElleDuzenlemeSonucu> KategoriAyarlaAsync(int id, string? kategori, CancellationToken ct)
    {
        var kod = string.IsNullOrWhiteSpace(kategori) ? null : kategori.Trim();
        if (kod is not null && !ProductAttributeParser.CategorySlugs.Contains(kod))
            return new ElleDuzenlemeSonucu(true, false, $"'{kod}' bir kategori değil.");

        var urun = await BulAsync(id, ct);
        if (urun is null)
            return ElleDuzenlemeSonucu.Yok;

        var simdi = DateTimeOffset.UtcNow;
        // Otomatiğe dönüş: yalnızca bayrak kalkıyor. Çıkarılan kategori bir sonraki
        // taramayla geri geliyor; burada çıkarmak taramanın kendi yedek kurallarını
        // atlar ve o taramayla çelişebilirdi.
        var guncellenen = kod is null
            ? await KardesSatirlar(urun.Value).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.CategoryIsManual, false), ct)
            : await KardesSatirlar(urun.Value).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Category, kod)
                .SetProperty(p => p.CategoryIsManual, true)
                .SetProperty(p => p.ContentUpdatedAt, simdi), ct);

        return new ElleDuzenlemeSonucu(true, true, GuncellenenSatir: guncellenen);
    }

    public async Task<ElleDuzenlemeSonucu> BesinAyarlaAsync(int id, ElleBesinIstegi istek, CancellationToken ct)
    {
        var urun = await BulAsync(id, ct);
        if (urun is null)
            return ElleDuzenlemeSonucu.Yok;

        var kontrol = Kontrol(istek);

        // AYNI ÜRÜNDE AYNI ENERJİ DEĞERİ İKİNCİ KEZ SORULMUYOR. Kutu her başarılı
        // kayıttan sonra sıfırlandığı için, etiketi çelişkili bir ürünü yeniden
        // düzenlemek onu yeniden reddettiriyordu; panelde tam bu yüzden bir kayıt
        // sessizce düştü (lif satırını silme kaydı). Karar zaten veride duruyor:
        // kayıtlı tablo elle girilmiş ve aynı enerjiyi taşıyor.
        if (!kontrol.Kabul && kontrol.RetKodu == "enerji-tutmuyor"
            && ZatenOnaylanmis(urun.Value.Tablo, urun.Value.Elle, istek.Kalori))
            kontrol = Kontrol(istek with { EtiketBoyleYaziyor = true });

        if (!kontrol.Kabul)
            return new ElleDuzenlemeSonucu(true, false, kontrol.RetSebebi, Kod: kontrol.RetKodu);

        var tablo = new Dictionary<string, string>();
        foreach (var (ad, deger) in kontrol.Satirlar)
            tablo[ad] = deger;
        var json = JsonSerializer.Serialize(tablo);
        var simdi = DateTimeOffset.UtcNow;

        // PAKET PORSİYONU KARDEŞ SATIRLARA YAZILMIYOR, tablo ve porsiyon yazılıyor.
        // Fark şu: aynı sayfanın 500 g ve 1 kg'lık boyları AYNI etiketi ve aynı
        // ölçeği taşır ama paketten çıkan porsiyon sayıları farklıdır; kardeşlere
        // kopyalamak 1 kg'lık kutuya 500 g'ın porsiyon sayısını yazardı.
        if (istek.PaketPorsiyonSayisi is { } paket)
            await db.Products.IgnoreQueryFilters().Where(p => p.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ServingsPerPackage, paket), ct);

        var guncellenen = await KardesSatirlar(urun.Value).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.NutritionJson, json)
            // Taban tablosunda porsiyon yok: servis başı hesaplar da porsiyon başı
            // protein de oluşmasın. Önceden kalmış bir porsiyon da siliniyor, yoksa
            // eski değerden servis hesabı sürerdi.
            .SetProperty(p => p.ProteinPerServingGrams, istek.PorsiyonBeyanYok ? null : istek.ProteinGram)
            .SetProperty(p => p.ServingSizeGrams, p => istek.PorsiyonBeyanYok ? null : istek.PorsiyonGram ?? p.ServingSizeGrams)
            .SetProperty(p => p.NutritionIsManual, true)
            .SetProperty(p => p.NutritionCheckedAt, simdi)
            .SetProperty(p => p.ContentUpdatedAt, simdi), ct);

        return new ElleDuzenlemeSonucu(true, true, GuncellenenSatir: guncellenen);
    }

    /// <summary>
    /// Tabloyu siler ve ürünü otomatik kaynaklara geri bırakır: detay tamamlama
    /// sayfaya yeniden bakar.
    /// </summary>
    public async Task<ElleDuzenlemeSonucu> BesinTemizleAsync(int id, CancellationToken ct)
    {
        var urun = await BulAsync(id, ct);
        if (urun is null)
            return ElleDuzenlemeSonucu.Yok;

        var simdi = DateTimeOffset.UtcNow;
        var guncellenen = await KardesSatirlar(urun.Value).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.NutritionJson, (string?)null)
            .SetProperty(p => p.ProteinPerServingGrams, (decimal?)null)
            .SetProperty(p => p.NutritionIsManual, false)
            .SetProperty(p => p.NutritionCheckedAt, (DateTimeOffset?)null)
            .SetProperty(p => p.ContentUpdatedAt, simdi), ct);

        return new ElleDuzenlemeSonucu(true, true, GuncellenenSatir: guncellenen);
    }

    /// <summary>
    /// "Bu üründe besin tablosu yok" — tabloyu siler ve ürünü otomatik kaynaklara
    /// GERİ BIRAKMAZ.
    /// </summary>
    /// <remarks>
    /// <see cref="BesinTemizleAsync"/> ile farkı burada: o, yanlış okunmuş bir tabloyu
    /// atıp kaynağa yeniden baktırmak için; bu ise kaynağın yayınlayacak bir şeyi
    /// olmadığına karar verildiğinde. Fark elle bakılmış olmak, o yüzden tablo boş
    /// olsa da <c>NutritionIsManual</c> işaretleniyor: yutma servisi ve detay
    /// tamamlama o bayrağa bakıp dokunmuyor, yoksa "kalıcı" sözü tutulmazdı.
    /// </remarks>
    public async Task<ElleDuzenlemeSonucu> BesinYokIsaretleAsync(int id, CancellationToken ct)
    {
        var urun = await BulAsync(id, ct);
        if (urun is null)
            return ElleDuzenlemeSonucu.Yok;

        var simdi = DateTimeOffset.UtcNow;
        var guncellenen = await KardesSatirlar(urun.Value).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.NutritionJson, (string?)null)
            .SetProperty(p => p.ProteinPerServingGrams, (decimal?)null)
            .SetProperty(p => p.NutritionIsManual, true)
            .SetProperty(p => p.NutritionCheckedAt, simdi)
            .SetProperty(p => p.ContentUpdatedAt, simdi), ct);

        return new ElleDuzenlemeSonucu(true, true, GuncellenenSatir: guncellenen);
    }

    // Satırlarda izin verilen birimler. Kapalı liste: "5 gr" ya da "200 mgs" sitede
    // başka hiçbir yerde olmayan bir yazımla yayınlanmak yerine listeyle reddediliyor.
    // 24'lü kutu, 60 kapsül, 120 servislik kova: üstü yazım hatası.
    private const int EnFazlaPaketPorsiyonu = 500;

    private static readonly string[] Birimler = ["g", "mg", "mcg", "IU", "milyar CFU"];

    // Makroların kendi alanları var ve orada kalori kontrolünden geçiyorlar; serbest
    // satır olarak yazılsalar o kontrolü atlarlardı. Yaygın yazımlarıyla birlikte.
    /// <summary>Porsiyon beyanı olmayan tablonun taban satırının adı ("Değerler: 100 g başına").</summary>
    internal const string TabanSatiri = "Değerler";

    private static readonly HashSet<string> MakroAdlari = new[]
    {
        "porsiyon", "değerler", "enerji", "kalori", "yağ", "toplam yağ", "karbonhidrat", "toplam karbonhidrat",
        "lif", "diyet lifi", "protein",
    }.Select(Katla).ToHashSet(StringComparer.Ordinal);

    private static string Katla(string metin) =>
        metin.Replace('İ', 'i').Replace('I', 'i').Replace('ı', 'i').ToLowerInvariant();

    private const int EnFazlaSatir = 40;
    private const int EnUzunAd = 60;

    // Porsiyon başına makul en büyük miktar (100.000 mcg biyotin var; spor
    // etiketlerinde bunun üstü yok). Fazladan yazılan sıfırı yakalıyor.
    private const decimal EnBuyukMiktar = 100_000m;

    /// <summary>Yazılan değerlerin yayınlayacağı tablo ya da ret sebebi.</summary>
    /// <remarks>
    /// <b>İki tür tablo.</b> Besin değeri tablosu (protein tozu, bar): enerji, protein,
    /// karbonhidrat ve yağ birlikte, kalori kontrolüyle. Takviye tablosu (kreatin, amino
    /// asit, vitamin): kontrol edilecek kalorisi olmayan adlı satırlar; her satır kendi
    /// başına kontrol ediliyor. 17 Eylül'de ölçüldü: besin değeri eksik satırlar protein
    /// tozunda 910, amino asitte 603, vitaminde 605.
    /// </remarks>
    internal static ElleBesinKontrolu Kontrol(ElleBesinIstegi istek)
    {
        decimal?[] degerler = [istek.PorsiyonGram, istek.Kalori, istek.ProteinGram, istek.KarbonhidratGram, istek.YagGram, istek.LifGram];
        if (degerler.Any(v => v < 0))
            return Ret("değerler negatif olamaz");

        if (istek.PaketPorsiyonSayisi is { } paketPorsiyon && (paketPorsiyon < 1 || paketPorsiyon > EnFazlaPaketPorsiyonu))
            return Ret($"pakette porsiyon 1 ile {EnFazlaPaketPorsiyonu} arasında olmalı");

        var (digerSatirlar, satirHatasi) = SatirlariKontrolEt(istek.DigerSatirlar ?? [], istek.PorsiyonGram);
        if (satirHatasi is not null)
            return Ret(satirHatasi);

        // Kontrol edilecek bir kalori hesabı ancak protein/karbonhidrat/yağ varsa
        // kurulabiliyor; enerjinin kendisi tek başına yazılabilir.
        decimal?[] besinler = [istek.ProteinGram, istek.KarbonhidratGram, istek.YagGram];
        var besinVar = besinler.Any(v => v is not null);

        var satirlar = new List<(string, string)>();
        // PORSİYON BEYANI YOKSA gram değeri porsiyon değil TABLONUN TABANI ("100 g
        // başına", "50 g başına"): markalar farklı tabanlar kullanıyor. Porsiyon diye
        // yazılsaydı site ondan servis sayısı ve servis başı maliyet hesaplardı, yani
        // markanın hiç beyan etmediği bir porsiyon uydurmuş olurduk.
        if (istek.PorsiyonBeyanYok && istek.PorsiyonGram is null)
            return Ret("değerler kaç gram başına yazıyorsa porsiyon kutusuna o sayıyı gir (ör. 100 ya da 50)");
        if (istek.PorsiyonGram is { } porsiyon)
            satirlar.Add(istek.PorsiyonBeyanYok
                ? (TabanSatiri, Yaz(porsiyon, "g") + " başına")
                : ("Porsiyon", Yaz(porsiyon, "g")));

        if (besinVar)
        {
            // Eksik olanı ADIYLA söylüyor. Önceki metin yalnızca kuralı
            // tekrarlıyordu ("dördünü de girin") ve hangi kutunun boş kaldığını
            // söylemediği için etiketi 0 kcal yazan üründe iki kez aynı duvara
            // çarpıldı: eksik olan tek şey 0 yazılmamış enerjiydi.
            var eksikler = new[]
            {
                istek.Kalori is null ? "enerji" : null,
                istek.ProteinGram is null ? "protein" : null,
                istek.KarbonhidratGram is null ? "karbonhidrat" : null,
                istek.YagGram is null ? "yağ" : null,
            }.Where(a => a is not null).ToArray();
            if (eksikler.Length > 0)
                return Ret(
                    $"boş kalan: {string.Join(", ", eksikler)}. Bu dördü birlikte girilir; etikette 0 yazıyorsa 0 gir. " +
                    "Takviye tablosu (kreatin, amino asit, vitamin) için dördünü de boş bırak.",
                    "makro-eksik");

            var imkansiz = ImkansizDegerler(istek.ProteinGram!.Value, istek.KarbonhidratGram!.Value, istek.YagGram!.Value, istek.LifGram, istek.PorsiyonGram);
            if (imkansiz is not null)
                return Ret(imkansiz);

            // Enerji uyuşmazlığı YAZIM HATASI koruması, imkansızlık değil: bazı
            // etiketler (özellikle BCAA/EAA) 10 g protein yazıp enerjiyi 0 kcal
            // beyan ediyor. Kural kapalı kalsaydı tek çıkış ya uydurma bir kalori
            // yazmak ya da protein satırını hiç yayınlamamak olurdu; ikisi de
            // etiketten uzaklaşmak demek. O yüzden atlanabiliyor, ama kendiliğinden
            // değil: kişinin "etiket böyle yazıyor" demesi gerekiyor.
            var enerjiHatasi = KaloriKontrolu(istek.Kalori!.Value, istek.ProteinGram!.Value, istek.KarbonhidratGram!.Value, istek.YagGram!.Value, istek.LifGram);
            if (enerjiHatasi is not null && !istek.EtiketBoyleYaziyor)
                return Ret(enerjiHatasi + "; etiket gerçekten böyle yazıyorsa 'etiket böyle yazıyor' kutusunu işaretle", "enerji-tutmuyor");

            satirlar.Add(("Enerji", Yaz(istek.Kalori.Value, "kcal")));
            satirlar.Add(("Yağ", Yaz(istek.YagGram.Value, "g")));
            satirlar.Add(("Karbonhidrat", Yaz(istek.KarbonhidratGram.Value, "g")));
            if (istek.LifGram is { } lif)
                satirlar.Add(("Lif", Yaz(lif, "g")));
            satirlar.Add(("Protein", Yaz(istek.ProteinGram.Value, "g")));
        }
        else if (istek.Kalori is { } yalnizEnerji)
        {
            // YALNIZ ENERJİ. Bazı etiketler (ör. içilebilir amino ürünleri) enerjiyi
            // yazıp protein/karbonhidrat/yağ satırı basmıyor. Dördünü birlikte
            // şart koşmak burada gerçek bir değeri yayınlanamaz yapıyordu; tek
            // çıkış ya enerjiyi atmak ya da olmayan üç satırı uydurmaktı.
            // Kalori kontrolü YOK, çünkü karşılaştırılacak makro da yok.
            satirlar.Add(("Enerji", Yaz(yalnizEnerji, "kcal")));
            if (istek.LifGram is { } lif)
                satirlar.Add(("Lif", Yaz(lif, "g")));
        }
        else
        {
            if (digerSatirlar.Count == 0 && istek.LifGram is null)
                return Ret("enerji, protein, karbonhidrat ve yağı ya da etiketten en az bir satır girin");
            if (istek.LifGram is { } lif)
                satirlar.Add(("Lif", Yaz(lif, "g")));
        }

        satirlar.AddRange(digerSatirlar);
        return new ElleBesinKontrolu(satirlar, null);
    }

    // AB etiketi: karbonhidrat lifi içermiyor, lif 2 kcal/g. Yine de bazı etiketler
    // (özellikle ithal ürünler) ABD usulü lifi karbonhidratın içinde yazıyor; iki
    // okumanın arasındaki her değer kabul. Tolerans: kalori 5-10'a, makrolar grama
    // yuvarlanıyor; toplamda ±15 kcal ya da %10.
    // Etiketin yazamayacağı değerler: hangi kutu işaretlenirse işaretlensin geçmiyor,
    // çünkü bunlar etiketin tuhaflığı değil yazım hatasının kendisi.
    private static string? ImkansizDegerler(decimal protein, decimal karbonhidrat, decimal yag, decimal? lif, decimal? porsiyon)
    {
        if (protein > 100)
            return $"protein {protein} g makul değil";

        // Lif karbonhidratın İÇİNDE yazılmış olabilir (ABD usulü, bazı ithal ve
        // yerli etiketler): o zaman bir kez sayılır. Lif karbonhidrattan büyükse
        // içinde olamaz, ayrı sayılır. Kalori kontrolü iki usulü de baştan beri
        // kabul ediyordu; bu kural yalnızca Avrupa usulünü bildiği için
        // Animal Joy Whey Nut'ın gerçek etiketini (100 g: P 30,11, K 31,06, Y 33,42,
        // Lif 21,92) "116 g" diye reddediyordu — lif iki kez sayılıyordu.
        var ayriLif = lif is { } l && l > karbonhidrat ? l : 0m;
        var makroToplam = protein + karbonhidrat + yag + ayriLif;
        return porsiyon is > 0 && makroToplam > porsiyon * 1.05m + 1
            ? $"makrolar ({makroToplam} g) porsiyonu ({porsiyon} g) aşıyor"
            : null;
    }

    private static string? KaloriKontrolu(decimal kalori, decimal protein, decimal karbonhidrat, decimal yag, decimal? lif)
    {
        var temel = 4 * protein + 4 * karbonhidrat + 9 * yag;
        var ust = temel + 2 * (lif ?? 0);
        // ABD usulünde lif karbonhidratın içinde ve kalorisi 0-2 kcal/g (çözünmeyen
        // lif 0). Alt sınır 0 ile: Whey Nut'ın 448,9 kcal'i ancak böyle tutuyor.
        var alt = temel - 4 * Math.Min(karbonhidrat, lif ?? 0);
        var tolerans = Math.Max(15m, kalori * 0.1m);

        return kalori < alt - tolerans || kalori > ust + tolerans
            ? $"enerji {kalori} kcal makrolarla tutmuyor (beklenen {alt:0}-{ust:0})"
            : null;
    }

    private static (List<(string, string)> Satirlar, string? Hata) SatirlariKontrolEt(
        IReadOnlyList<ElleBesinSatiri> satirlar, decimal? porsiyonGram)
    {
        if (satirlar.Count > EnFazlaSatir)
            return ([], $"en fazla {EnFazlaSatir} satır girilebilir");

        var sonuc = new List<(string, string)>();
        var gorulen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var satir in satirlar)
        {
            var ad = string.Join(' ', (satir.Ad ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (ad.Length == 0)
                return ([], "bir satırın adı yok");
            if (ad.Length > EnUzunAd)
                return ([], $"'{ad[..20]}…' {EnUzunAd} karakterden uzun");

            // Karşılaştırma için bütün i/ı/İ/I biçimleri "i"ye katlanıyor: satır adları
            // Türkçe ("LİF") de İngilizce ("PROTEIN") de yazılıyor; tr-TR küçültme
            // "PROTEIN"i "proteın", invariant küçültme "LİF"i "lİf" yapardı.
            var katlanmis = Katla(ad);
            if (MakroAdlari.Contains(katlanmis))
                return ([], $"'{ad}' için yukarıda ayrı bir alan var; oraya girin");
            if (!gorulen.Add(katlanmis))
                return ([], $"'{ad}' iki kez yazılmış");

            if (satir.Miktar is not { } miktar)
                return ([], $"'{ad}' için miktar yok");
            // SIFIR KABUL: etiket "Şeker 0 g" yazıyorsa bu bir değerdir, eksiklik
            // değil. Boş satır bu kontrole hiç gelmiyor (miktarsız satır arayüzde
            // düşüyor, gelirse üstteki kontrol yakalıyor), yani 0'ı reddetmenin tek
            // yaptığı şey etiketin yazdığını yasaklamaktı.
            if (miktar < 0)
                return ([], $"'{ad}' negatif olamaz");
            if (miktar > EnBuyukMiktar)
                return ([], $"'{ad}' {miktar} yazım hatası gibi görünüyor");

            var birim = Birimler.FirstOrDefault(b => string.Equals(b, satir.Birim?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (birim is null)
                return ([], $"'{ad}': birim şunlardan biri olmalı: {string.Join(", ", Birimler)}");

            // 5 g'lık ölçekte 50 g kreatin kaymış bir virgüldür, etiket değil.
            if (birim == "g" && porsiyonGram is > 0 && miktar > porsiyonGram * 1.05m + 1)
                return ([], $"'{ad}' ({miktar} g) porsiyondan ({porsiyonGram} g) büyük");

            sonuc.Add((ad, Yaz(miktar, birim)));
        }

        return (sonuc, null);
    }

    // Türkçe yazım: "24,1 g", "120 kcal". Site Türkçe ve otomatik okunan tablolar
    // kaynağın yazımını koruduğu için zaten virgüllü ("1,50 gr"); nokta kullanmak
    // aynı sayfada iki ayrı ondalık işareti demekti. Geri okuma iki biçimi de
    // tanıyor (bkz. besin-satirlari.ts), yani düzenleme bozulmuyor.
    private static readonly CultureInfo TurkceYazim = CultureInfo.GetCultureInfo("tr-TR");

    private static string Yaz(decimal miktar, string birim) =>
        miktar.ToString("0.###", TurkceYazim) + " " + birim;

    /// <summary>
    /// Bu ürünün çelişkili enerjisi daha önce elle onaylanmış mı. Yalnızca ELLE
    /// girilmiş tabloya bakıyor: otomatik okunmuş bir tablodaki 0 kcal kimsenin
    /// kararı değil, onu onay saymak kontrolü kendiliğinden kapatırdı. Enerji
    /// değişirse yeniden soruluyor.
    /// </summary>
    internal static bool ZatenOnaylanmis(string? mevcutJson, bool elle, decimal? kalori)
    {
        if (!elle || mevcutJson is null || kalori is null)
            return false;

        Dictionary<string, string>? tablo;
        try
        {
            tablo = JsonSerializer.Deserialize<Dictionary<string, string>>(mevcutJson);
        }
        catch (JsonException)
        {
            return false;
        }

        return tablo is not null
            && tablo.TryGetValue("Enerji", out var mevcut)
            && mevcut == Yaz(kalori.Value, "kcal");
    }

    private static ElleBesinKontrolu Ret(string sebep, string? kod = null) => new([], sebep, kod);

    private async Task<(int MarkaId, string? Satici, string Adres, string? Tablo, bool Elle)?> BulAsync(int id, CancellationToken ct)
    {
        var satir = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.BrandId, p.Seller, p.Url, p.NutritionJson, p.NutritionIsManual })
            .FirstOrDefaultAsync(ct);
        return satir is null ? null : (satir.BrandId, satir.Seller, satir.Url, satir.NutritionJson, satir.NutritionIsManual);
    }

    // Aynı ürün sayfasının bütün satırları: aynı marka ve satıcı, "?" sonrası hariç aynı adres.
    private IQueryable<Product> KardesSatirlar((int MarkaId, string? Satici, string Adres, string? Tablo, bool Elle) urun)
    {
        var sayfa = urun.Adres.Split('?', 2)[0];
        var onek = sayfa + "?";
        var markaId = urun.MarkaId;
        var satici = urun.Satici;
        return db.Products.IgnoreQueryFilters()
            .Where(p => p.BrandId == markaId && p.Seller == satici && (p.Url == sayfa || p.Url.StartsWith(onek)));
    }
}

using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// "Hangi takviye?" sayfalarındaki ürün listesinin aday satırı: sorgudan
/// belleğe alınan ham hâli.
/// </summary>
public sealed record ValuePickCandidate(
    int ProductId,
    int BrandId,
    string BrandName,
    string ProductName,
    string? Size,
    decimal Price,
    bool? InStock);

public sealed record RankedValuePick(ValuePickCandidate Candidate, decimal PricePerKg);

/// <summary>Seçilen ürünler ve kilogram fiyatı hesaplanabilen toplam ürün sayısı.</summary>
public sealed record ValuePickRanking(IReadOnlyList<RankedValuePick> Picks, int EligibleCount);

/// <summary>
/// Bir kategorideki ürünleri KİLOGRAM FİYATINA göre sıralar, her markadan bir
/// ürün seçer.
///
/// NEDEN KİLOGRAM (29 Eylül, canlı DB): protein tozlarının yalnızca %9'unda
/// porsiyon + protein verisi var. "Protein başına en ucuz" sıralaması
/// ürünlerin %91'ini dışarıda bırakıp yanlış bir "en ucuz" iddiası kurardı.
/// Paket ağırlığı ise %80'inde biliniyor.
///
/// NEDEN KORUMALAR: ham kilogram sıralaması canlıda listenin başına yanlış
/// ürünleri taşıyordu. Protein tozunda ilk sıralar "Cream of Rice" (pirinç unu),
/// proteinli makarna ve 60 g'lık tek porsiyon paketti. Kilo-hacimde boyutu
/// "3000 Kg" yazılmış bir ürün 0 TL/kg çıkıyordu. Kreatinde gainer + kreatin
/// paketleri vardı, ayrıca 244 protein tozu stokta değildi. Kategori
/// etiketinin kendisi bazen yanlış: bayinin kategorisi ürünü taşıyor, marka
/// adındaki "protein" de ProteinOcean'ın kreatinini ve pirinç ununu protein
/// tozu yapıyor. Bu yüzden kategori tek başına yetmiyor, ürünün biçimine de
/// bakılıyor. Kurallar kuru çalıştırmayla canlı adaylar üzerinde denendi.
///
/// HER MARKADAN BİR ÜRÜN: aynı kreatin üç bayide altı satır olarak duruyordu,
/// listenin yarısı tek ürün olurdu. Markanın en uygun kg fiyatlı ürünü
/// seçiliyor. Bu bir gösterim kuralı; sayfa ölçütü açıkça yazıyor.
/// </summary>
public static partial class ValuePickRanker
{
    public const int DefaultCount = 6;
    public const int MaxCount = 12;

    private sealed record KategoriKurali(decimal MinGram, decimal MaxGram, Regex? FarkliUrun);

    // Paket aralıkları canlı veriye göre (29 Eylül). Alt sınır tek porsiyonluk
    // saşeleri ve atıştırmalıkları (granola 270 g, makarna 240 g, 60 g'lık
    // paket) dışarıda bırakıyor; üst sınır "3000 Kg" gibi birim hatalarını.
    // Tadımlık küçük paketler zaten kg fiyatında pahalı, alt sınır asıl
    // olarak ucuz görünen yanlış ürünleri eliyor.
    private static readonly Dictionary<string, KategoriKurali> Kurallar = new(StringComparer.Ordinal)
    {
        ["protein-tozu"] = new(400m, 6000m, ProteinTozuOlmayanRegex()),
        ["kreatin"] = new(100m, 2000m, KreatinKarisimiRegex()),
        ["kilo-hacim"] = new(1000m, 10000m, null),
        ["enerji-jeli-sporcu-icecekleri"] = new(200m, 5000m, null),
    };

    private static readonly KategoriKurali VarsayilanKural = new(100m, 10000m, null);

    /// <summary>
    /// Kategori içinde daraltma türleri. Protein tozunda testin süt ürünü
    /// sorusu (izole / bitkisel); kilo-hacimde gainer ile saf karbonhidrat
    /// tozu ayrı listeleniyor, çünkü tek listede kg fiyatına göre ilk altının
    /// tamamı pirinç unu ve maltodekstrindi (gainer'lar 7. sıradan sonra).
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, Func<string, bool>>> Turler = new(StringComparer.Ordinal)
    {
        ["protein-tozu"] = new(StringComparer.Ordinal)
        {
            // İzole/hidrolize whey'de laktoz çok düşük; "İzole & Konsantre"
            // karışımı konsantre taşıdığı için bu listeye girmiyor.
            ["izole"] = ad => IzoleRegex().IsMatch(ad) && !KonsantreRegex().IsMatch(ad),
            ["bitkisel"] = ad => BitkiselRegex().IsMatch(ad),
        },
        ["kilo-hacim"] = new(StringComparer.Ordinal)
        {
            ["gainer"] = GainerMi,
            ["karbonhidrat"] = ad => !GainerMi(ad),
        },
    };

    /// <summary>Tür bu kategoride tanımlı mı? (Uç, serbest değeri önbelleğe sokmamak için soruyor.)</summary>
    public static bool IsValidType(string category, string type) =>
        Turler.TryGetValue(category, out var turler) && turler.ContainsKey(type);

    public static ValuePickRanking Rank(
        IEnumerable<ValuePickCandidate> candidates, string category, string? type, int count)
    {
        var kural = Kurallar.GetValueOrDefault(category, VarsayilanKural);
        Func<string, bool> tureUyar = type is null
            ? _ => true
            : Turler.GetValueOrDefault(category)?.GetValueOrDefault(type) ?? (_ => false);

        var uygunlar = candidates
            // Stok bilgisi vermeyen kaynak (null) listede kalıyor: bilinmeyeni
            // "stokta yok" saymak da uydurma olurdu.
            .Where(c => c.InStock != false && c.Price > 0)
            .Select(c => (Aday: c, Gram: DealsQueryService.ParsePackageGrams(c.Size)))
            .Where(x => x.Gram is decimal gram && gram >= kural.MinGram && gram <= kural.MaxGram)
            .Select(x => (x.Aday, x.Gram, Ad: DealsQueryService.NormalizeSearchText(x.Aday.ProductName)))
            .Where(x => !PaketMi(x.Aday.ProductName, x.Ad))
            .Where(x => kural.FarkliUrun is null || !kural.FarkliUrun.IsMatch(x.Ad))
            .Where(x => tureUyar(x.Ad))
            .Select(x => new RankedValuePick(x.Aday, Math.Round(x.Aday.Price / x.Gram!.Value * 1000m, 2)))
            // Id eşitlik bozucu: aynı kg fiyatlı ürünlerin sırası istekten
            // isteğe değişmesin (önbellek ve SSR aynı listeyi göstersin).
            .OrderBy(x => x.PricePerKg)
            .ThenBy(x => x.Candidate.ProductId)
            .ToList();

        var secilenler = uygunlar
            .DistinctBy(x => x.Candidate.BrandId)
            .Take(Math.Clamp(count, 1, MaxCount))
            .ToList();

        return new ValuePickRanking(secilenler, uygunlar.Count);
    }

    // Çok ürünlü setler: "... Paketi" / "... Seti" (BundleProductFilter) ve
    // "Mass Gainer 3000 G + Creatine" gibi artıyla birleştirilmiş adlar.
    private static bool PaketMi(string hamAd, string normalAd) =>
        Scraping.BundleProductFilter.IsBundle(hamAd) || normalAd.Contains(" + ", StringComparison.Ordinal);

    private static bool GainerMi(string ad) => GainerRegex().IsMatch(ad) && !SafKarbonhidratRegex().IsMatch(ad);

    // Protein tozu kategorisine düşmüş başka ürünler (canlı adaylarda
    // görülenler): kreatin, pirinç unu, gainer, kolajen, puding, pankek/waffle
    // karışımı, çorba, öğün tozu, recovery karışımı.
    // AROMA ADLARI BİLEREK YOK: "Cookies & Cream", "Brownie", "Bebe Bisküvisi",
    // "Birthday Cake" protein tozlarının aroması; bunları aramak gerçek
    // ürünleri silerdi. Aynı biçimdeki atıştırmalıklar (kurabiye 330 g,
    // pankek 50 g) zaten 400 g alt sınırında kalıyor. "karbonhidrat tozu" iki
    // kelime: tek başına "karbonhidrat" "düşük karbonhidratlı" bir proteini de
    // silerdi. Kolajen tam bir protein değil; kas için önerilen listede yeri yok.
    // Desenler NormalizeSearchText'ten geçmiş ada uygulanıyor: noktasız ı
    // "i"ye katlanmış hâlde ("Pirinç Kreması" -> "pirinç kremasi").
    [GeneratedRegex(@"creatine|kreatin|cream of rice|pirinç kremasi|pirinc kremasi|gainer|\bmass\b|karbonhidrat tozu|kolajen|collagen|granola|makarna|pudd?ing|pancake|pankek|waffle|çorba|corba|\bmeal\b|öğün tozu|ogun tozu|recovery")]
    private static partial Regex ProteinTozuOlmayanRegex();

    // Kreatin monohidrat önerisi için karışımlar dışarıda: gainer/protein +
    // kreatin, "Crea Carb", "Kreatin Glutamin Taurin", "Creatine ... Amino
    // Asit". Marka adının içindeki "protein" ("Proteinocean Creatine") kelime
    // sınırı sayesinde eşleşmiyor.
    [GeneratedRegex(@"gainer|\bmass\b|\bwhey\b|\bprotein\b|karbonhidrat|\bcarb\b|glutamin|taurin|bcaa|amino|arginin|arjinin")]
    private static partial Regex KreatinKarisimiRegex();

    [GeneratedRegex(@"isolate|izole|\biso\b|isowhey|hydro|hidroliz")]
    private static partial Regex IzoleRegex();

    [GeneratedRegex(@"konsantre|concentrate")]
    private static partial Regex KonsantreRegex();

    [GeneratedRegex(@"vegan|bitkisel|plant|bezelye|\bpea\b|\bsoya?\b|kenevir|hemp|pirinç proteini|pirinc proteini|rice protein")]
    private static partial Regex BitkiselRegex();

    // "bulk": kütle artırıcıların ortak adı ("GNC Pro Bulk 1340"), kategori
    // ayrıştırıcısında da kilo-hacim kelimesi.
    [GeneratedRegex(@"gainer|\bmass\b|\bgain\b|promass|gainzilla|\bbulk\b")]
    private static partial Regex GainerRegex();

    // Tek bir karbonhidrat kaynağını adıyla söyleyen ürün gainer değil.
    // "Karbonhidrat Tozu" BİLEREK burada yok: bayiler onu kategori adı olarak
    // gainer'ların adına ekliyor ("Optimum Serious Mass Karbonhidrat Tozu";
    // kuru çalıştırmada bilinen üç gainer bu yüzden yanlış listeye düştü).
    [GeneratedRegex(@"maltodextrin|maltodekstrin|cream of rice|pirinç|pirinc|dextrose|dekstroz|vitargo")]
    private static partial Regex SafKarbonhidratRegex();
}

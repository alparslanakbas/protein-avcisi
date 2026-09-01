using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Tek kutuda BİRDEN ÇOK ÜRÜN satan setleri tanır.
///
/// Bunlar takviye dışı değil — içindekiler gerçek ürün — ama tek bir fiyat
/// noktası olarak saklamak iki şeyi bozuyor: servis başı fiyat hesabı
/// (kaç servis olduğu belirsiz) ve "gerçek indirim" karşılaştırması (paketin
/// içeriği zamanla değişebiliyor). İlk olarak Provitamin'de karar verildi
/// (`2b61b56`), sonra GNC ve Supra Protein'de de aynı durum çıktı.
///
/// Kalıp DAR tutuluyor: yalnızca "paket"/"set" sözcüğünün kendisi, ek almış
/// hâlleriyle. Katalog tarandı (2 Eylül 2026) — "set" gövdesi mevcut 2009
/// ürünün HİÇBİRİNDE geçmiyor, dolayısıyla eklenmesi bir şeyi taşımıyor.
/// </summary>
public static partial class BundleProductFilter
{
    /// <summary>
    /// Ürün adı çok ürünlü bir seti mi anlatıyor?
    ///
    /// Türkçe harf tuzağı: "PAKETİ" içindeki noktalı İ, OrdinalIgnoreCase ile
    /// "i"ye katlanmıyor; noktalı/noktasız ayrımı önce siliniyor.
    /// </summary>
    public static bool IsBundle(string name)
    {
        var normalized = name.Replace('İ', 'i').Replace('I', 'ı').ToLowerInvariant();
        return BundleWordRegex().IsMatch(normalized);
    }

    /// <summary>
    /// İYELİK EKİ ŞART — çıplak "paket" ARANMIYOR. Katalogda
    /// "Buster Preworkout ... 22.4 gram Tek Paket Servis" var: bu TEK
    /// servislik bir ürün, set değil. "paket" tek başına aransaydı elenirdi.
    ///
    /// Kelime sınırı da şart: eksiz alt dize araması "korseti", "reset",
    /// "preset" gibi kelimelerin içine denk gelirdi.
    /// </summary>
    [GeneratedRegex(@"\b(paketi|seti)\b")]
    private static partial Regex BundleWordRegex();
}

using System.Text.RegularExpressions;

namespace IndirimTakip.Infrastructure.Scraping;

/// <summary>
/// Tek kutuda BİRDEN ÇOK ÜRÜN satan setleri tanır.
///
/// Bunlar takviye dışı değil — içindekiler gerçek ürün — ama tek bir fiyat
/// DİKKAT — BU GENEL BİR POLİTİKA DEĞİL. Sitenin yerleşik davranışı paketleri
/// TUTMAK: 2 Eylül'de ölçüldü, canlıda dokuz markada 102 paket ürünü duruyor
/// (BigJoy 36, West Nutrition 25, Xpro 13...) ve hiçbirinde servis verisi
/// olmadığı için servis başı fiyat hesabını da bozmuyorlar.
///
/// Süzgeç yalnızca paketleri AYNI ŞEYİN KOPYALARI olan kaynaklarda kullanılıyor.
/// Şu an tek kullanıcı Provitamin: oradaki 15 adres bir ürün ailesinin beden
/// varyantları ve numaralı tekrarları (fitness-paketi-small-4, -medium-2,
/// -large-6...). Yeni bir kaynağa eklemeden önce o kaynağın paketlerinin
/// gerçekten ayrı ürünler mi yoksa kopyalar mı olduğuna BAKILMALI.
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

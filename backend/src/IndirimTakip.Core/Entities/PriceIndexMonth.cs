namespace IndirimTakip.Core.Entities;

/// <summary>
/// Aylık fiyat endeksinin bir ay × kategori satırı: aynı ürünlerin bu ayki tipik fiyatının bir önceki aya oranı.
/// </summary>
/// <remarks>
/// PriceHistories'ten TÜRETİLİYOR (FiyatEndeksiServisi). Biten ay bir kez yazılıp bir daha hesaplanmıyor; içinde
/// bulunulan ay geçici olarak her taramadan sonra yeniden yazılıyor. Böylece hesap her seferinde yalnız son iki ayın
/// fiyatlarını okuyor; bütün geçmişi taramak (21 Ekim'de 1,5 sn) her ay biraz daha yavaşlardı.
/// </remarks>
public class PriceIndexMonth
{
    /// <summary>Bütün kategorilerin birlikte hesaplandığı satırın <see cref="Category"/> değeri.</summary>
    public const string AllCategories = "tum";

    public int Id { get; set; }

    /// <summary>Ayın ilk günü (Türkiye takvimi).</summary>
    public DateOnly Month { get; set; }

    /// <summary>Kategori slug'ı ya da <see cref="AllCategories"/>.</summary>
    public required string Category { get; set; }

    /// <summary>Ürün başına (bu ay / önceki ay) oranlarının geometrik ortalaması (Jevons).</summary>
    public decimal Ratio { get; set; }

    public int ProductCount { get; set; }
    public int BrandCount { get; set; }
    public int RisenCount { get; set; }
    public int FallenCount { get; set; }

    /// <summary>Oranı üç kattan büyük değiştiği için dışarıda bırakılan ürünler (büyük olasılıkla paket ya da okuma).</summary>
    public int ExcludedCount { get; set; }

    /// <summary>Ay bitmeden hesaplandı; ay bitince kesin değerle yeniden yazılıyor.</summary>
    public bool IsProvisional { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}

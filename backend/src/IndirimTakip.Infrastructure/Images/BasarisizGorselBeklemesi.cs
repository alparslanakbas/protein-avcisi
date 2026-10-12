namespace IndirimTakip.Infrastructure.Images;

/// <summary>
/// İndirilemeyen görselin ürününü bir süre aday listesinden çıkarır.
/// </summary>
/// <remarks>
/// Başarısız görselin yerel yolu boş kaldığı için ürün her turda, yani 10
/// dakikada bir yeniden aday oluyordu: ölü bir adrese günde 144 istek
/// (12 Ekim'de canlıda: biri 404 dönen adres, biri ImageSharp'ın okumadığı
/// AVIF). Bekleme yalnızca bellekte; deploy onu sıfırlarsa adres bir kez
/// daha denenir, kayıp bundan ibaret. Ürün kimliğine bağlı: kaynak görsel
/// adresini değiştirirse yeni adres bekleme bitince denenir.
/// </remarks>
internal sealed class BasarisizGorselBeklemesi
{
    private readonly Dictionary<int, DateTimeOffset> basarisizlar = [];

    public void BasarisizOldu(int urunId, DateTimeOffset an) => basarisizlar[urunId] = an;

    /// <summary>Süresi dolanları bırakır, hâlâ beklemede olan ürünleri döndürür.</summary>
    public IReadOnlySet<int> Bekleyenler(DateTimeOffset an, TimeSpan sure)
    {
        foreach (var id in basarisizlar.Where(k => an - k.Value >= sure).Select(k => k.Key).ToList())
            basarisizlar.Remove(id);
        return basarisizlar.Keys.ToHashSet();
    }
}

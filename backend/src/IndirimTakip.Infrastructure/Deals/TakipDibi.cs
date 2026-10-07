namespace IndirimTakip.Infrastructure.Deals;

/// <summary>
/// "… beri en düşük" rozeti: güncel fiyat, takip başladığından beri gördüğümüz en düşük fiyat mı?
/// </summary>
/// <remarks>
/// <b>"Son 30 günün en düşüğü" rozetinin üst kademesi, ayrı bir tanım değil.</b> Yalnızca "güncel fiyat bütün
/// geçmişin en düşüğü ve geçmişte daha pahalıydı" kuralı canlıda 415 ürün seçiyordu (7 Ekim), ama zamdan sonra
/// olağan fiyatına dönen ürünü de dipte sayıyordu (3 Ekim'de "gerçek indirim"de kapatılan hatanın aynısı), aylardır
/// değişmeyen yeni bir fiyatı da. Otuz günün rozeti ikisini olağan fiyatla zaten eliyor; bu kural onun üstüne en az
/// <see cref="EnAzGecmisGun"/> günlük geçmiş ve bütün geçmişin en düşüğü şartını koyuyor. Canlıda 330 rozetin
/// 298'i bu kademede: takip 10 Ağustos'ta başladığı için ikisi şimdilik çoğunlukla örtüşüyor, geçmiş uzadıkça bu
/// rozet seçicileşir.
///
/// Rozet tarihi söylüyor ("10 Ağustos'tan beri"): "tüm zamanların en düşüğü" diyemeyiz, takipten öncesini bilmiyoruz.
/// </remarks>
public static class TakipDibi
{
    /// <summary>"Takip başladığından beri" diyebilmek için gereken en kısa geçmiş.</summary>
    public const int EnAzGecmisGun = 30;

    /// <returns>Rozet geçerliyse takip başlangıcı, değilse null.</returns>
    public static DateTimeOffset? Baslangic(
        bool otuzGununEnDusugu,
        decimal guncelFiyat,
        DateTimeOffset? takipBaslangici,
        decimal? takipEnDusuk,
        DateTimeOffset simdi) =>
        otuzGununEnDusugu
        && takipBaslangici is { } baslangic && baslangic <= simdi.AddDays(-EnAzGecmisGun)
        && takipEnDusuk is { } enDusuk && guncelFiyat <= enDusuk
            ? baslangic
            : null;
}

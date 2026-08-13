namespace IndirimTakip.Core.Entities;

// "Haber Ver" — kullanıcı belirli bir ürünün fiyatı düştüğünde tek
// seferlik bir bildirim almak istiyor. Tam bir hesap/kalıcı takip listesi
// sistemi DEĞİL (roadmap'te "tam üyelik" ayrı ve daha büyük bir madde
// olarak bekliyor) — mevcut Subscriber (double opt-in) altyapısını
// kullanıyor. Bildirim gönderilince NotifiedAt set edilip "tüketiliyor",
// tekrar tekrar bildirim gitmiyor.
public class ProductWatch
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? NotifiedAt { get; set; }
}

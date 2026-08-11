namespace IndirimTakip.Core.Entities;

// Markanın sitede otomatik olarak uygulanmayan, kullanıcının elle girmesi gereken
// kampanya kodları (örn. SSN "YENIYIL" %25). Otomatik scrape edilmiyor — kodlar
// sık değişiyor ve yanlış/süresi geçmiş bir kod göstermek kullanıcıyı ödeme anında
// gerçekten yanıltır. Bu yüzden elle girilip elle doğrulanıyor.
public class Coupon
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public Brand? Brand { get; set; }

    public required string Code { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public DateTimeOffset LastVerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

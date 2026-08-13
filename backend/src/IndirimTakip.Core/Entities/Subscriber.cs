namespace IndirimTakip.Core.Entities;

// E-posta bülteni aboneleri. Double opt-in zorunlu (İYS/KVKK gereği,
// bkz. CLAUDE.md) — abone olurken IsConfirmed=false ile oluşturuluyor,
// Token'a giden onay linkine tıklayınca true'ya çevriliyor. Aynı Token
// hem onay hem abonelikten çıkma linkinde kullanılıyor.
public class Subscriber
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Token { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTimeOffset SubscribedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? UnsubscribedAt { get; set; }
}

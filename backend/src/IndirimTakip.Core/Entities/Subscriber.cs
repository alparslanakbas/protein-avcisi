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
    public DateTimeOffset? LastConfirmationEmailSentAt { get; set; }
    // Favori listesi kurtarma maili (bkz. FavoriteService.SendRecoveryEmailAsync)
    // için ayrı bir cooldown alanı — onay mailiyle aynı amaç ama farklı akış,
    // ikisinin birbirini sıfırlamaması için ayrı tutuluyor.
    public DateTimeOffset? LastRecoveryEmailSentAt { get; set; }
    // Bültenin bu aboneye en son ne zaman gittiği. Zamanlamanın TEK kaynağı
    // bu alan — bellekteki bir sayaç değil, çünkü o her deploy/restart'ta
    // sıfırlanıyordu ve bülten hiç gönderilemiyordu. Ayrıca "kim bu haftanın
    // bültenini henüz almadı" sorusunu da cevapladığı için, günlük gönderim
    // kotası aşıldığında kalan aboneler ertesi gün kaldığı yerden devam
    // ediyor (bkz. DigestService).
    public DateTimeOffset? LastDigestSentAt { get; set; }
}

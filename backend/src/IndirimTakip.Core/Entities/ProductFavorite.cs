namespace IndirimTakip.Core.Entities;

// Hesap/login gerektirmeyen hafif "favorilerim" listesi — Subscriber'ın
// (e-posta + token) aynı altyapısına bağlı ama e-posta hiç gönderilmediği
// için onay akışına (IsConfirmed) hiç girmiyor.
public class ProductFavorite
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

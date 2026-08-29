using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Subscribers;

/// <summary>
/// E-posta adresinin biçimini ve alan adının gerçekten var olup olmadığını
/// kontrol eder.
///
/// Biçim kontrolü tek başına yetmiyordu: uydurma ama biçimsel olarak geçerli
/// adresler (forma mesaj yazan biri, ya da bir bot) onay e-postası
/// gönderilmesine yol açıyor. Bu hem kotadan yiyor hem de geri dönen
/// postalar gönderen itibarını düşürüyor.
/// </summary>
public class EmailAddressValidator(ILogger<EmailAddressValidator> logger)
{
    /// <summary>
    /// Tek kullanımlık/atılabilir posta sağlayıcıları. Uzun bir liste tutmanın
    /// anlamı yok (sürekli yenileri çıkıyor); yalnızca yaygın olanlar.
    /// </summary>
    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", "tempmail.com",
        "temp-mail.org", "yopmail.com", "throwawaymail.com", "getnada.com",
        "trashmail.com", "sharklasers.com", "maildrop.cc", "dispostable.com",
    };

    /// <summary>
    /// Alan adı çözümlemesi için üst sınır. Gerçek bir kullanıcıyı
    /// bekletmemek için kısa; aşılırsa adres KABUL ediliyor (bkz. aşağıda).
    /// </summary>
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(3);

    public async Task<bool> IsDeliverableAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        // MailAddress boşluk içeren adresleri (alıntılanmış yerel kısım
        // kuralı yüzünden) geçerli sayıyor; pratikte böyle bir adres hep
        // hatalı yazım ya da çöp oluyor.
        if (email.Any(char.IsWhiteSpace))
            return false;

        string domain;
        try
        {
            var parsed = new MailAddress(email.Trim());
            domain = parsed.Host;
        }
        catch (FormatException)
        {
            return false;
        }

        if (domain.Length == 0 || !domain.Contains('.'))
            return false;

        if (DisposableDomains.Contains(domain))
            return false;

        return await DomainResolvesAsync(domain, cancellationToken);
    }

    /// <summary>
    /// Alan adının çözümlenip çözümlenmediğine bakar.
    ///
    /// DNS'in kendisi hata verirse ya da zaman aşımına uğrarsa adres KABUL
    /// EDİLİYOR (fail-open): geçici bir ağ sorunu yüzünden gerçek bir
    /// kullanıcının aboneliğini engellemek, birkaç sahte adresi kabul
    /// etmekten daha kötü.
    /// </summary>
    private async Task<bool> DomainResolvesAsync(string domain, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DnsTimeout);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domain, timeout.Token);
            return addresses.Length > 0;
        }
        catch (SocketException)
        {
            // Alan adı yok — aradığımız durum bu.
            return false;
        }
        catch (Exception e) when (e is OperationCanceledException or ArgumentException)
        {
            logger.LogInformation("Alan adı çözümlenemedi, adres kabul ediliyor: {Domain}", domain);
            return true;
        }
    }
}

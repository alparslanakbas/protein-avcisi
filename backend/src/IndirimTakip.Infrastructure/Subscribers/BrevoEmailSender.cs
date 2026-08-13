using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace IndirimTakip.Infrastructure.Subscribers;

// Brevo'nun transactional email API'si (günde 300, süresiz ücretsiz —
// bkz. CLAUDE.md'deki karşılaştırma). Gönderen adres domain'in Brevo'da
// SPF/DKIM ile doğrulanmasını gerektiriyor, aksi halde e-postalar spam'e
// düşebiliyor ya da hiç gönderilemiyor.
public class BrevoEmailSender(HttpClient httpClient, IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Brevo:ApiKey"];
        var senderEmail = configuration["Brevo:SenderEmail"] ?? "bulten@proteinavcisi.com.tr";

        var payload = new
        {
            sender = new { name = "Protein Avcısı", email = senderEmail },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent = htmlBody,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", apiKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

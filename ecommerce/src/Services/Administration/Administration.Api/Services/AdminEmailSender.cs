using System.Net;
using System.Net.Mail;

namespace Administration.Api.Services;

public sealed class AdminEmailSender(IConfiguration configuration)
{
    public async Task SendResetAsync(string address, string token, CancellationToken ct)
    {
        var host = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host)) return;
        var baseUrl = configuration["Email:ResetBaseUrl"] ?? "";
        var link = $"{baseUrl.TrimEnd('/')}/Account/Reset?email={Uri.EscapeDataString(address)}&token={Uri.EscapeDataString(token)}";
        using var message = new MailMessage(configuration["Email:From"] ?? "noreply@example.invalid", address, "Restablecer acceso administrativo", $"Utiliza este enlace durante la próxima hora: {link}");
        using var client = new SmtpClient(host, configuration.GetValue("Email:SmtpPort", 587)) { EnableSsl = configuration.GetValue("Email:SmtpUseSsl", true) };
        var username = configuration["Email:SmtpUsername"];
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, configuration["Email:SmtpPassword"]);
        await client.SendMailAsync(message, ct);
    }
}

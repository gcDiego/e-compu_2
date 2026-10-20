using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Administration.Api.Services;

public sealed class AdminEmailSender(IConfiguration configuration)
{
    public async Task SendResetAsync(string address, string token, CancellationToken ct)
    {
        var host = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host)) return;
        var baseUrl = configuration["Email:ResetBaseUrl"] ?? "";
        var link = $"{baseUrl.TrimEnd('/')}/Account/Reset?email={Uri.EscapeDataString(address)}&token={Uri.EscapeDataString(token)}";
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(configuration["Email:From"] ?? "noreply@example.invalid"));
        message.To.Add(MailboxAddress.Parse(address));
        message.Subject = "Restablecer acceso administrativo";
        message.Body = new TextPart("plain") { Text = $"Utiliza este enlace durante la próxima hora: {link}{Environment.NewLine}{Environment.NewLine}Token: {token}" };
        using var client = new SmtpClient { CheckCertificateRevocation = configuration.GetValue("Email:CheckCertificateRevocation", true) };
        var useSsl = configuration.GetValue("Email:SmtpUseSsl", true);
        await client.ConnectAsync(host, configuration.GetValue("Email:SmtpPort", 587), useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None, ct);
        var username = configuration["Email:SmtpUsername"];
        if (!string.IsNullOrWhiteSpace(username)) await client.AuthenticateAsync(username, configuration["Email:SmtpPassword"] ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

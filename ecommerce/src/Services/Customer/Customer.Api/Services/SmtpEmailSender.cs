using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Customer.Api.Services;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    private readonly string _host = configuration["Email:SmtpHost"] ?? string.Empty;
    private readonly int _port = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;
    private readonly string _username = configuration["Email:SmtpUsername"] ?? string.Empty;
    private readonly string _password = configuration["Email:SmtpPassword"] ?? string.Empty;
    private readonly string _from = configuration["Email:From"] ?? "noreply@example.com";
    private readonly string _fromName = configuration["Email:FromName"] ?? "Ecommerce";
    private readonly SecureSocketOptions _secure = configuration.GetValue<bool?>("Email:SmtpUseSsl") is true
        ? SecureSocketOptions.SslOnConnect
        : SecureSocketOptions.StartTls;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
            throw new InvalidOperationException("Email SMTP settings are not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        client.CheckCertificateRevocation = false;
        await client.ConnectAsync(_host, _port, _secure, ct);
        await client.AuthenticateAsync(_username, _password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

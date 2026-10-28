using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Order.Api.Services;

public sealed class OrderEmailSender(IConfiguration configuration, ILogger<OrderEmailSender> logger)
{
    public async Task SendOrderStatusNotificationAsync(
        string customerEmail,
        string customerName,
        int orderId,
        string transactionId,
        string status,
        string? courier,
        string? trackingNumber,
        string? comment,
        CancellationToken ct)
    {
        var host = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("SMTP no configurado en Order.Api. Notificación para orden #{OrderId} omitida (modo demo).", orderId);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(configuration["Email:From"] ?? "pedidos@paduki.demo"));
            message.To.Add(MailboxAddress.Parse(customerEmail));
            message.Subject = $"Actualización de tu pedido #{orderId} - {StatusLabel(status)}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Hola {customerName},");
            sb.AppendLine();
            sb.AppendLine($"El estado de tu pedido #{orderId} (Transacción: {transactionId}) ha cambiado a: {StatusLabel(status).ToUpperInvariant()}.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(courier))
                sb.AppendLine($"Paquetería: {courier}");
            if (!string.IsNullOrWhiteSpace(trackingNumber))
                sb.AppendLine($"Número de guía / rastreo: {trackingNumber}");
            if (!string.IsNullOrWhiteSpace(comment))
                sb.AppendLine($"Notas adicionales: {comment}");

            sb.AppendLine();
            sb.AppendLine("Gracias por comprar en Ecommerce Paduki.");

            message.Body = new TextPart("plain") { Text = sb.ToString() };

            using var client = new SmtpClient { CheckCertificateRevocation = configuration.GetValue("Email:CheckCertificateRevocation", true) };
            var useSsl = configuration.GetValue("Email:SmtpUseSsl", true);
            await client.ConnectAsync(host, configuration.GetValue("Email:SmtpPort", 587), useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None, ct);
            var username = configuration["Email:SmtpUsername"];
            if (!string.IsNullOrWhiteSpace(username))
                await client.AuthenticateAsync(username, configuration["Email:SmtpPassword"] ?? "", ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            logger.LogInformation("Correo de estado enviado a {Email} para orden #{OrderId}.", customerEmail, orderId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar correo de estado para orden #{OrderId}.", orderId);
        }
    }

    private static string StatusLabel(string status) => status.ToLowerInvariant() switch
    {
        "confirmado" => "Confirmado",
        "en_proceso" => "En preparación",
        "enviado" => "Enviado",
        "entregado" => "Entregado",
        "cancelado" => "Cancelado",
        _ => status
    };
}

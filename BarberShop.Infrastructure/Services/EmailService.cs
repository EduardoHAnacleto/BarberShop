using BarberShop.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BarberShop.Infrastructure.Services;

// SMTP mail sender. Real delivery requires Email:Enabled=true plus SMTP
// credentials — neither is set by default, so a fresh clone (or this demo)
// logs the message instead of sending it rather than failing startup or
// silently dropping mail. This mirrors the Swagger:Enabled / DOCKERHUB_ENABLED
// pattern used elsewhere in this project for portfolio-safe defaults.
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (!_config.GetValue("Email:Enabled", false))
        {
            _logger.LogInformation(
                "Email delivery disabled (Email:Enabled=false) — would have sent {Subject} to {ToEmail}:\n{Body}",
                subject, toEmail, htmlBody);
            return;
        }

        var fromAddress = _config["Email:FromAddress"] ?? "noreply@barbershop.com";
        var fromName = _config["Email:FromName"] ?? "BarberShop";
        var host = _config["Email:SmtpHost"];
        var port = _config.GetValue("Email:SmtpPort", 587);
        var user = _config["Email:SmtpUser"];
        var password = _config["Email:SmtpPassword"];

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Email:Enabled is true but Email:SmtpHost is not configured — skipping send");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);

        if (!string.IsNullOrWhiteSpace(user))
            await client.AuthenticateAsync(user, password ?? string.Empty);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email {Subject} sent to {ToEmail}", subject, toEmail);
    }
}

using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ShormanServicesBackend.Api.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Shorman";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

public interface IEmailSender
{
    bool IsConfigured { get; }

    /// <returns>true when the message was handed to the SMTP server.</returns>
    Task<bool> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions emailOptions = options.Value;

    public bool IsConfigured => emailOptions.IsConfigured;

    public async Task<bool> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!emailOptions.IsConfigured)
        {
            logger.LogWarning("Email is not configured (Email:Host / Email:FromAddress). Message to {Recipient} was not sent.", toAddress);
            return false;
        }

        try
        {
            using var client = new SmtpClient(emailOptions.Host, emailOptions.Port)
            {
                EnableSsl = emailOptions.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            if (!string.IsNullOrWhiteSpace(emailOptions.Username))
            {
                client.Credentials = new NetworkCredential(emailOptions.Username, emailOptions.Password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(emailOptions.FromAddress, emailOptions.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toAddress);

            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send email to {Recipient}.", toAddress);
            return false;
        }
    }
}

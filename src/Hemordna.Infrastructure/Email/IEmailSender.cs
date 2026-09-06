namespace Hemordna.Infrastructure.Email;

/// <summary>Sends a single transactional e-mail. No queueing or retry - see ResendEmailSender.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken);
}

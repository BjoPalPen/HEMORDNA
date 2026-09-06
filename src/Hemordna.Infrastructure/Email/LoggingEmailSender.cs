using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Hemordna.Infrastructure.Email;

/// <summary>
/// Records the most recent e-mail sent to each address instead of actually sending it. Used
/// when no Resend API key is configured (local development, and the test fixture) so a
/// password-reset link can still be exercised - see DevEmailOutbox and its dev-only endpoint
/// in Program.cs. Never registered when a real API key is present.
/// </summary>
public sealed class DevEmailOutbox
{
    private readonly ConcurrentDictionary<string, string> _lastBodyByEmail = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string toEmail, string htmlBody) => _lastBodyByEmail[toEmail] = htmlBody;

    public string? LastBodyFor(string toEmail) => _lastBodyByEmail.GetValueOrDefault(toEmail);
}

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly DevEmailOutbox _outbox;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, DevEmailOutbox outbox)
    {
        _logger = logger;
        _outbox = outbox;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        _outbox.Record(toEmail, htmlBody);
        _logger.LogInformation(
            "No Resend API key configured - logging e-mail instead of sending.\nTo: {To}\nSubject: {Subject}\n{Body}",
            toEmail, subject, htmlBody);

        return Task.CompletedTask;
    }
}

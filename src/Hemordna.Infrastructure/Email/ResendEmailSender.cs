using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Hemordna.Infrastructure.Email;

/// <summary>Sends transactional e-mail through Resend (api.resend.com). Registered only when
/// <see cref="ResendOptions.ApiKey"/> is configured - see DependencyInjection.</summary>
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly ResendOptions _options;

    public ResendEmailSender(HttpClient http, IOptions<ResendOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{_options.FromName} <{_options.FromAddress}>",
                to = new[] { toEmail },
                subject,
                html = htmlBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

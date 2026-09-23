using System.Net;
using System.Text.Json;
using Hemordna.Application.Push;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;
using HemordnaPushSubscription = Hemordna.Domain.Push.PushSubscription;

namespace Hemordna.Infrastructure.Services;

/// <summary>
/// Sends Web Push notifications via the <c>WebPush</c> package - verified running on net10.0 in
/// BowlingPlatform (same stack, same author). No scheduling, no notification types, no policy:
/// see <see cref="IPushSender"/>.
/// </summary>
internal sealed class WebPushSender : IPushSender
{
    private readonly IPushSubscriptionRepository _subscriptions;
    private readonly ILogger<WebPushSender> _logger;
    private readonly WebPushClient _client;
    private readonly VapidDetails _vapid;
    private readonly string _publicKey;

    public WebPushSender(
        IPushSubscriptionRepository subscriptions,
        IConfiguration configuration,
        ILogger<WebPushSender> logger)
    {
        _subscriptions = subscriptions;
        _logger = logger;

        var publicKey = configuration["Vapid:PublicKey"]
            ?? throw new InvalidOperationException("Vapid:PublicKey is not configured.");
        var privateKey = configuration["Vapid:PrivateKey"]
            ?? throw new InvalidOperationException("Vapid:PrivateKey is not configured.");
        var subject = configuration["Vapid:Subject"]
            ?? throw new InvalidOperationException("Vapid:Subject is not configured.");

        _publicKey = publicKey;
        _vapid = new VapidDetails(subject, publicKey, privateKey);
        _client = new WebPushClient();
    }

    public string GetVapidPublicKey() => _publicKey;

    public async Task<int> SendAsync(
        IReadOnlyList<HemordnaPushSubscription> subscriptions,
        string title,
        string body,
        string? url,
        CancellationToken cancellationToken)
    {
        // Deliberately just these three fields - no notificationId, no badge, no quick-reply
        // actions. Nothing here reads a reply back (CLAUDE.md's scope note for this task), so
        // there is nothing for those to wire up to yet.
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            url = url ?? "/"
        });

        var successCount = 0;
        List<HemordnaPushSubscription>? expired = null;

        foreach (var subscription in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPush.PushSubscription(
                    subscription.Endpoint, subscription.P256dh, subscription.Auth);

                var options = new Dictionary<string, object>
                {
                    ["vapidDetails"] = _vapid
                };

                await _client.SendNotificationAsync(pushSubscription, payload, options, cancellationToken);
                successCount++;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                // 410 Gone / 404 Not Found - the push service no longer knows this subscription.
                // Never log title/body (CLAUDE.md §10) - only the subscription's own id.
                _logger.LogInformation(
                    "Push subscription {SubscriptionId} is no longer valid ({StatusCode}), removing it.",
                    subscription.Id, (int)ex.StatusCode);
                (expired ??= []).Add(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Push delivery failed for subscription {SubscriptionId}.", subscription.Id);
            }
        }

        if (expired is not null)
        {
            foreach (var subscription in expired)
            {
                await _subscriptions.RemoveAsync(subscription, cancellationToken);
            }
        }

        return successCount;
    }
}

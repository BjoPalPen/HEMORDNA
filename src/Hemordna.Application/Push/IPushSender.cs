using Hemordna.Domain.Push;

namespace Hemordna.Application.Push;

/// <summary>
/// Sends a Web Push notification to a set of already-resolved subscriptions. No scheduling, no
/// policy, no notion of notification types - see CLAUDE.md's scope note for this task. An
/// implementation is expected to remove a subscription itself once the push service reports it
/// gone (HTTP 410/404).
/// </summary>
public interface IPushSender
{
    /// <summary>The VAPID public key the client needs to create a subscription.</summary>
    string GetVapidPublicKey();

    /// <summary>
    /// Sends to every subscription in <paramref name="subscriptions"/>. Returns the number that
    /// actually accepted the push - a failed or expired delivery does not count, so the caller
    /// can tell "sent" from "nothing reached".
    /// </summary>
    Task<int> SendAsync(
        IReadOnlyList<PushSubscription> subscriptions,
        string title,
        string body,
        string? url,
        CancellationToken cancellationToken);
}

using Hemordna.Domain.Push;

namespace Hemordna.Application.Push;

/// <summary>
/// Registers (or re-registers) a device's Web Push subscription for the caller. The endpoint is
/// unique across the whole table - see <see cref="PushSubscription.Reclaim"/> - so subscribing
/// again from the same browser install updates the existing row instead of duplicating it.
/// </summary>
public sealed class SubscribeToPush
{
    private readonly IPushSubscriptionRepository _subscriptions;
    private readonly TimeProvider _timeProvider;

    public SubscribeToPush(IPushSubscriptionRepository subscriptions, TimeProvider timeProvider)
    {
        _subscriptions = subscriptions;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        Guid householdId,
        Guid memberId,
        string endpoint,
        string p256dh,
        string auth,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var existing = await _subscriptions.FindByEndpointAsync(endpoint, cancellationToken);

        if (existing is not null)
        {
            existing.Reclaim(householdId, memberId, p256dh, auth, now);
            await _subscriptions.UpdateAsync(existing, cancellationToken);
            return;
        }

        var subscription = PushSubscription.Subscribe(householdId, memberId, endpoint, p256dh, auth, now);
        await _subscriptions.AddAsync(subscription, cancellationToken);
    }
}

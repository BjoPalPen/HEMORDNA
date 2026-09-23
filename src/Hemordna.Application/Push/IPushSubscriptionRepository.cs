using Hemordna.Domain.Push;

namespace Hemordna.Application.Push;

/// <summary>Persists members' Web Push subscriptions.</summary>
public interface IPushSubscriptionRepository
{
    Task AddAsync(PushSubscription subscription, CancellationToken cancellationToken);

    /// <summary>The subscription for this endpoint, if any - endpoint is unique across the
    /// whole table (not scoped by household), since a browser install has exactly one.</summary>
    Task<PushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken cancellationToken);

    Task UpdateAsync(PushSubscription subscription, CancellationToken cancellationToken);

    Task RemoveAsync(PushSubscription subscription, CancellationToken cancellationToken);

    /// <summary>All of this member's own devices - what a test or a future real notification
    /// sends to.</summary>
    Task<IReadOnlyList<PushSubscription>> ListForMemberAsync(
        Guid householdId, Guid memberId, CancellationToken cancellationToken);
}

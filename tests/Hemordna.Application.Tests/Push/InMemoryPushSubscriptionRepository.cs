using Hemordna.Application.Push;
using Hemordna.Domain.Push;

namespace Hemordna.Application.Tests.Push;

internal sealed class InMemoryPushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly List<PushSubscription> _subscriptions = [];

    internal int AddCallCount { get; private set; }

    internal int UpdateCallCount { get; private set; }

    internal int RemoveCallCount { get; private set; }

    internal void Seed(PushSubscription subscription) => _subscriptions.Add(subscription);

    public Task AddAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _subscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public Task<PushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken cancellationToken)
        => Task.FromResult(_subscriptions.FirstOrDefault(s => s.Endpoint == endpoint));

    public Task UpdateAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        RemoveCallCount++;
        _subscriptions.Remove(subscription);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PushSubscription>> ListForMemberAsync(
        Guid householdId, Guid memberId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PushSubscription>>([.. _subscriptions
            .Where(s => s.HouseholdId == householdId && s.MemberId == memberId)]);
}

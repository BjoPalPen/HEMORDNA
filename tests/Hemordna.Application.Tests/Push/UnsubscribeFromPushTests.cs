using Hemordna.Application.Push;
using Hemordna.Domain.Push;

namespace Hemordna.Application.Tests.Push;

public class UnsubscribeFromPushTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private const string Endpoint = "https://push.example.com/subscribe/abc123";

    private readonly InMemoryPushSubscriptionRepository _subscriptions = new();

    private UnsubscribeFromPush CreateUseCase() => new(_subscriptions);

    [Fact]
    public async Task Removes_the_callers_own_subscription()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, Endpoint, "p256dh-key", "auth-secret", Now));

        await CreateUseCase().HandleAsync(householdId, memberId, Endpoint, CancellationToken.None);

        Assert.Equal(1, _subscriptions.RemoveCallCount);
        Assert.Null(await _subscriptions.FindByEndpointAsync(Endpoint, CancellationToken.None));
    }

    [Fact]
    public async Task Does_nothing_for_an_unknown_endpoint()
    {
        await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), Endpoint, CancellationToken.None);

        Assert.Equal(0, _subscriptions.RemoveCallCount);
    }

    [Fact]
    public async Task Does_not_remove_another_members_subscription()
    {
        var householdId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, ownerMemberId, Endpoint, "p256dh-key", "auth-secret", Now));

        await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), Endpoint, CancellationToken.None);

        Assert.Equal(0, _subscriptions.RemoveCallCount);
        Assert.NotNull(await _subscriptions.FindByEndpointAsync(Endpoint, CancellationToken.None));
    }

    [Fact]
    public async Task Does_not_remove_the_same_members_subscription_in_another_household()
    {
        var memberId = Guid.NewGuid();
        _subscriptions.Seed(PushSubscription.Subscribe(
            Guid.NewGuid(), memberId, Endpoint, "p256dh-key", "auth-secret", Now));

        await CreateUseCase().HandleAsync(Guid.NewGuid(), memberId, Endpoint, CancellationToken.None);

        Assert.Equal(0, _subscriptions.RemoveCallCount);
    }
}

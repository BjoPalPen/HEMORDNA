using Hemordna.Application.Push;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Push;

namespace Hemordna.Application.Tests.Push;

public class SubscribeToPushTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private const string Endpoint = "https://push.example.com/subscribe/abc123";

    private readonly InMemoryPushSubscriptionRepository _subscriptions = new();

    private SubscribeToPush CreateUseCase() => new(_subscriptions, new FixedTimeProvider(Now));

    [Fact]
    public async Task Adds_a_new_subscription_for_a_new_endpoint()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await CreateUseCase().HandleAsync(
            householdId, memberId, Endpoint, "p256dh-key", "auth-secret", CancellationToken.None);

        Assert.Equal(1, _subscriptions.AddCallCount);
        Assert.Equal(0, _subscriptions.UpdateCallCount);

        var stored = await _subscriptions.FindByEndpointAsync(Endpoint, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(householdId, stored.HouseholdId);
        Assert.Equal(memberId, stored.MemberId);
        Assert.Equal(Now, stored.CreatedAt);
    }

    [Fact]
    public async Task Re_subscribing_the_same_endpoint_updates_instead_of_duplicating()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var earlier = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, Endpoint, "old-p256dh", "old-auth", earlier));

        await CreateUseCase().HandleAsync(
            householdId, memberId, Endpoint, "new-p256dh", "new-auth", CancellationToken.None);

        Assert.Equal(0, _subscriptions.AddCallCount);
        Assert.Equal(1, _subscriptions.UpdateCallCount);

        var stored = await _subscriptions.FindByEndpointAsync(Endpoint, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("new-p256dh", stored.P256dh);
        Assert.Equal("new-auth", stored.Auth);
        Assert.Equal(Now, stored.LastSeenAt);
    }

    [Fact]
    public async Task A_different_member_subscribing_the_same_endpoint_reassigns_ownership()
    {
        var originalHousehold = Guid.NewGuid();
        var originalMember = Guid.NewGuid();
        _subscriptions.Seed(PushSubscription.Subscribe(
            originalHousehold, originalMember, Endpoint, "p256dh-key", "auth-secret", Now.AddDays(-1)));

        var newHousehold = Guid.NewGuid();
        var newMember = Guid.NewGuid();

        await CreateUseCase().HandleAsync(
            newHousehold, newMember, Endpoint, "p256dh-key", "auth-secret", CancellationToken.None);

        var stored = await _subscriptions.FindByEndpointAsync(Endpoint, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newHousehold, stored.HouseholdId);
        Assert.Equal(newMember, stored.MemberId);
    }
}

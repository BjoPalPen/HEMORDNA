using Hemordna.Domain.Push;

namespace Hemordna.Domain.Tests;

public class PushSubscriptionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private const string Endpoint = "https://push.example.com/subscribe/abc123";

    private static PushSubscription CreateSubscription(
        Guid? householdId = null,
        Guid? memberId = null,
        string endpoint = Endpoint,
        string p256dh = "p256dh-key",
        string auth = "auth-secret")
        => PushSubscription.Subscribe(
            householdId ?? Guid.NewGuid(), memberId ?? Guid.NewGuid(), endpoint, p256dh, auth, CreatedAt);

    [Fact]
    public void A_new_subscription_carries_its_creation_time_as_last_seen()
    {
        var subscription = CreateSubscription();

        Assert.Equal(CreatedAt, subscription.CreatedAt);
        Assert.Equal(CreatedAt, subscription.LastSeenAt);
    }

    [Fact]
    public void Household_and_member_ids_are_set_from_creation()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var subscription = CreateSubscription(householdId: householdId, memberId: memberId);

        Assert.Equal(householdId, subscription.HouseholdId);
        Assert.Equal(memberId, subscription.MemberId);
    }

    [Fact]
    public void An_empty_household_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateSubscription(householdId: Guid.Empty));
    }

    [Fact]
    public void An_empty_member_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateSubscription(memberId: Guid.Empty));
    }

    [Fact]
    public void A_blank_endpoint_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateSubscription(endpoint: "   "));
    }

    [Fact]
    public void A_non_https_endpoint_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateSubscription(endpoint: "http://push.example.com/abc"));
    }

    [Fact]
    public void A_relative_endpoint_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateSubscription(endpoint: "/subscribe/abc123"));
    }

    [Fact]
    public void A_blank_key_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateSubscription(p256dh: " "));
        Assert.Throws<ArgumentException>(() => CreateSubscription(auth: " "));
    }

    [Fact]
    public void An_oversized_endpoint_is_rejected()
    {
        var oversized = "https://push.example.com/" + new string('a', PushSubscription.MaxEndpointLength);

        Assert.Throws<ArgumentException>(() => CreateSubscription(endpoint: oversized));
    }

    [Fact]
    public void Reclaim_reassigns_owner_and_refreshes_keys_and_last_seen()
    {
        var subscription = CreateSubscription();
        var newHouseholdId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var seenAt = CreatedAt.AddDays(3);

        subscription.Reclaim(newHouseholdId, newMemberId, "new-p256dh", "new-auth", seenAt);

        Assert.Equal(newHouseholdId, subscription.HouseholdId);
        Assert.Equal(newMemberId, subscription.MemberId);
        Assert.Equal("new-p256dh", subscription.P256dh);
        Assert.Equal("new-auth", subscription.Auth);
        Assert.Equal(seenAt, subscription.LastSeenAt);
        Assert.Equal(CreatedAt, subscription.CreatedAt);
    }

    [Fact]
    public void Reclaim_rejects_an_empty_household_or_member_id()
    {
        var subscription = CreateSubscription();

        Assert.Throws<ArgumentException>(() =>
            subscription.Reclaim(Guid.Empty, Guid.NewGuid(), "p256dh-key", "auth-secret", CreatedAt));
        Assert.Throws<ArgumentException>(() =>
            subscription.Reclaim(Guid.NewGuid(), Guid.Empty, "p256dh-key", "auth-secret", CreatedAt));
    }
}

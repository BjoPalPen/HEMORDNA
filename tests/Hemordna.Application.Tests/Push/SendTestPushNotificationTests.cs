using Hemordna.Application.Push;
using Hemordna.Domain.Push;

namespace Hemordna.Application.Tests.Push;

public class SendTestPushNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPushSubscriptionRepository _subscriptions = new();
    private readonly FakePushSender _sender = new();

    private SendTestPushNotification CreateUseCase() => new(_subscriptions, _sender);

    [Fact]
    public async Task Sends_to_every_one_of_the_callers_own_devices()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/a", "p256dh-key", "auth-secret", Now));
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/b", "p256dh-key", "auth-secret", Now));
        _sender.SentCount = 2;

        var sent = await CreateUseCase().HandleAsync(householdId, memberId, CancellationToken.None);

        Assert.Equal(2, sent);
        Assert.Equal(2, _sender.LastSubscriptions?.Count);
        Assert.Equal(SendTestPushNotification.Title, _sender.LastTitle);
        Assert.Equal(SendTestPushNotification.Body, _sender.LastBody);
    }

    [Fact]
    public async Task Never_sends_to_another_members_device()
    {
        var householdId = Guid.NewGuid();
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, Guid.NewGuid(), "https://push.example.com/a", "p256dh-key", "auth-secret", Now));

        var sent = await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Null(_sender.LastSubscriptions);
    }

    [Fact]
    public async Task Returns_zero_without_calling_the_sender_when_there_are_no_devices()
    {
        var sent = await CreateUseCase().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Null(_sender.LastSubscriptions);
    }
}

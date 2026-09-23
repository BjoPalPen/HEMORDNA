namespace Hemordna.Application.Push;

/// <summary>
/// Sends a fixed, generic test notification to every one of the caller's own devices - the only
/// way to trigger the push chain by hand in this task. There is deliberately no title or body
/// parameter: wiring push to a real event (a reminder, a task) is the next task, not this one.
/// </summary>
public sealed class SendTestPushNotification
{
    public const string Title = "Hemordna";
    public const string Body = "Det här är en testnotis. Om du ser den fungerar notiserna.";

    private readonly IPushSubscriptionRepository _subscriptions;
    private readonly IPushSender _sender;

    public SendTestPushNotification(IPushSubscriptionRepository subscriptions, IPushSender sender)
    {
        _subscriptions = subscriptions;
        _sender = sender;
    }

    /// <summary>Returns the number of devices that actually received it.</summary>
    public async Task<int> HandleAsync(Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var subscriptions = await _subscriptions.ListForMemberAsync(householdId, memberId, cancellationToken);

        return subscriptions.Count == 0
            ? 0
            : await _sender.SendAsync(subscriptions, Title, Body, "/", cancellationToken);
    }
}

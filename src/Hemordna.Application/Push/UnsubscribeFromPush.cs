namespace Hemordna.Application.Push;

/// <summary>
/// Removes a device's subscription. Only removes it when it actually belongs to the caller -
/// the same "not found means not yours" pattern <c>IReminderRepository.FindByIdAsync</c> uses -
/// so a caller can never remove another member's device merely by knowing (or guessing) its
/// endpoint. Idempotent: calling it twice, or for an endpoint that was never subscribed, is not
/// an error.
/// </summary>
public sealed class UnsubscribeFromPush
{
    private readonly IPushSubscriptionRepository _subscriptions;

    public UnsubscribeFromPush(IPushSubscriptionRepository subscriptions) => _subscriptions = subscriptions;

    public async Task HandleAsync(
        Guid householdId, Guid memberId, string endpoint, CancellationToken cancellationToken)
    {
        var existing = await _subscriptions.FindByEndpointAsync(endpoint, cancellationToken);

        if (existing is not null && existing.HouseholdId == householdId && existing.MemberId == memberId)
        {
            await _subscriptions.RemoveAsync(existing, cancellationToken);
        }
    }
}

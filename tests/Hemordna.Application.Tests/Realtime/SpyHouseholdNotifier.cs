using Hemordna.Application.Realtime;

namespace Hemordna.Application.Tests.Realtime;

/// <summary>Records notifications instead of actually pushing anything, so tests can assert on intent.</summary>
internal sealed class SpyHouseholdNotifier : IHouseholdNotifier
{
    private readonly List<Guid> _notifiedHouseholdIds = [];

    internal int CallCount => _notifiedHouseholdIds.Count;

    internal bool WasNotified(Guid householdId) => _notifiedHouseholdIds.Contains(householdId);

    public Task NotifyOccurrencesChangedAsync(Guid householdId, CancellationToken cancellationToken)
    {
        _notifiedHouseholdIds.Add(householdId);
        return Task.CompletedTask;
    }
}

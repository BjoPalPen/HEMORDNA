using Hemordna.Application.Realtime;

namespace Hemordna.Application.Households;

/// <summary>
/// Removes a member's day off - "Ångra ledig dag". Whatever was already moved out of the way
/// when the day off was set (<see cref="SetMemberDayOff"/>) stays exactly where it ended up;
/// clearing a day off is not an undo of everything that happened while it was in effect, only
/// of the day-off marking itself.
/// </summary>
public sealed class ClearMemberDayOff
{
    private readonly IHouseholdRepository _households;
    private readonly IMemberDayOffRepository _daysOff;
    private readonly IHouseholdNotifier _notifier;

    public ClearMemberDayOff(
        IHouseholdRepository households, IMemberDayOffRepository daysOff, IHouseholdNotifier notifier)
    {
        _households = households;
        _daysOff = daysOff;
        _notifier = notifier;
    }

    /// <summary>
    /// Clears the day off, or returns <c>null</c> when the household has no such member.
    /// Clearing a date that was never a day off is not an error - it is already what the
    /// caller wanted.
    /// </summary>
    public async Task<bool?> HandleAsync(
        Guid householdId, Guid memberId, DateOnly date, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (member is null)
        {
            return null;
        }

        await _daysOff.RemoveAsync(householdId, memberId, date, cancellationToken);

        // Nothing about any occurrence changed, but shared surfaces (Vecka/Hushåll's dot-off,
        // see docs/ARCHITECTURE.md) need the same coarse "something changed, re-fetch" signal
        // every other household-visible change already uses - see IHouseholdNotifier.
        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return true;
    }
}

using Hemordna.Application.Realtime;
using Hemordna.Application.Tasks;
using Hemordna.Domain.Common;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Households;

/// <summary>How a member's already-planned work on the day they are taking off should be
/// handled.</summary>
public enum DayOffMode
{
    /// <summary>Pull everything they have planned for that day onto today instead - "jobba i
    /// förväg" for the whole day at once (<see cref="TaskOccurrence.BringForwardTo"/>).</summary>
    BringAllForward,

    /// <summary>Push everything to the next day that is not also a day off for them.</summary>
    DeferAll
}

/// <summary>How many of the member's own occurrences on the day off actually moved.</summary>
public sealed record SetMemberDayOffResult(int BroughtForward, int Deferred, DateOnly? DeferredTo);

/// <summary>
/// Marks one date as a member's own day off - a hard exclusion from rotation
/// (<see cref="RotationPicker"/>), never a shared household setting (see docs/ARCHITECTURE.md
/// "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg", Del C). Whatever the
/// member already had planned for that date is moved out of the way according to
/// <see cref="DayOffMode"/> - a day off never just leaves a member's own outstanding work
/// sitting on a day they said they would not do it.
/// </summary>
public sealed class SetMemberDayOff
{
    private readonly IHouseholdRepository _households;
    private readonly IMemberDayOffRepository _daysOff;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IHouseholdNotifier _notifier;

    public SetMemberDayOff(
        IHouseholdRepository households,
        IMemberDayOffRepository daysOff,
        ITaskOccurrenceRepository occurrences,
        IHouseholdNotifier notifier)
    {
        _households = households;
        _daysOff = daysOff;
        _occurrences = occurrences;
        _notifier = notifier;
    }

    /// <summary>
    /// Sets the day off, or returns <c>null</c> when the household has no such member. Setting
    /// the same date a second time does not create a duplicate day off - by then nothing of the
    /// member's own is still scheduled on <paramref name="date"/> anyway (the first call already
    /// moved it), so a repeat call is naturally a no-op on the counts it returns.
    /// </summary>
    public async Task<SetMemberDayOffResult?> HandleAsync(
        Guid householdId, Guid memberId, DateOnly date, DateOnly today, DayOffMode mode, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (member is null)
        {
            return null;
        }

        if (await _daysOff.FindAsync(householdId, memberId, date, cancellationToken) is null)
        {
            await _daysOff.AddAsync(MemberDayOff.Create(householdId, memberId, date, today), cancellationToken);
        }

        var mine = (await _occurrences.ListOutstandingByHouseholdAsync(householdId, cancellationToken))
            .Where(occurrence => occurrence.AssignedMemberId == memberId && occurrence.ScheduledDate == date)
            .ToList();

        var broughtForward = 0;
        var deferred = 0;
        DateOnly? deferredTo = null;

        if (mode == DayOffMode.BringAllForward)
        {
            foreach (var occurrence in mine)
            {
                occurrence.BringForwardTo(today);
                await _occurrences.UpdateAsync(occurrence, cancellationToken);
                broughtForward++;
            }
        }
        else
        {
            // Only occurrences that CAN move at all are counted as "deferred" - one that cannot
            // be deferred simply stays exactly where it is; a day off does not force something
            // the household already said may never move.
            var movable = mine.Where(occurrence => occurrence.CanBeDeferred).ToList();

            if (movable.Count > 0)
            {
                var next = await FindNextAvailableDateAsync(householdId, memberId, date, cancellationToken);

                foreach (var occurrence in movable)
                {
                    occurrence.DeferTo(next);
                    await _occurrences.UpdateAsync(occurrence, cancellationToken);
                    deferred++;
                }

                deferredTo = next;
            }
        }

        if (broughtForward > 0 || deferred > 0)
        {
            await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);
        }

        return new SetMemberDayOffResult(broughtForward, deferred, deferredTo);
    }

    /// <summary>The first date after <paramref name="date"/>, within 14 days, that is not also a
    /// day off for this member.</summary>
    private async Task<DateOnly> FindNextAvailableDateAsync(
        Guid householdId, Guid memberId, DateOnly date, CancellationToken cancellationToken)
    {
        for (var offset = 1; offset <= 14; offset++)
        {
            var candidate = date.AddDays(offset);

            if (await _daysOff.FindAsync(householdId, memberId, candidate, cancellationToken) is null)
            {
                return candidate;
            }
        }

        throw new DomainException(
            "Hittade ingen ledig dag inom 14 dagar att flytta uppgifterna till - använd Pausa i stället.");
    }
}

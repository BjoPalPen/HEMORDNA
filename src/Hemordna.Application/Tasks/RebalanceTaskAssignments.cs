using Hemordna.Application.Households;
using Hemordna.Application.Realtime;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Reassigns already-outstanding rotating task occurrences so the household's current
/// workload matches each active member's current share of the household's total capacity
/// (<see cref="WeeklyTimeBudget.TotalWeeklyMinutes"/>) - the same signal
/// <see cref="RotationPicker"/> already weighs every new pick against, just applied once,
/// deliberately, to work that already has an owner.
/// </summary>
/// <remarks>
/// <b>Why this exists.</b> <see cref="RotationPicker"/> only ever decides ONE new occurrence
/// at a time, as it is generated - it never revisits a past decision. If a household's members'
/// capacities change (a role picked later, a new member joins, a pause ends, or - the case that
/// motivated this - a household's target capacity ratio itself changes, see docs/ARCHITECTURE.md
/// "65/35 target split"), everything already sitting on someone's plate stays exactly where it
/// is until it is completed, skipped, or deferred - the new ratio only ever applies to work
/// generated AFTER the change. This use case is the deliberate, explicit, one-off counterpart:
/// given the CURRENT capacities, move only the occurrences that need to move to bring what is
/// already outstanding back in line.
/// <para>
/// <b>What can move.</b> Only occurrences that are still outstanding
/// (<see cref="TaskOccurrenceStatus.Planned"/>), whose <see cref="TaskDefinition"/> is still
/// active, and whose responsibility actually rotates
/// (<see cref="TaskDefinition.HasRotatingResponsibility"/>). Completed and skipped occurrences
/// are never touched - they are done, not up for negotiation. An occurrence under a deactivated
/// ("archived") definition is left alone entirely, same reasoning. A FIXED task (one with a
/// <see cref="TaskDefinition.DefaultResponsibleMemberId"/> instead of rotation, e.g. someone's
/// own bedroom) is never a candidate for reassignment at all - the household explicitly chose a
/// permanent owner for it, which functions as an implicit opt-out from automatic rebalancing.
/// The domain has no separate "locked assignment" or "manual vs automatic" concept as of this
/// writing (see docs/ARCHITECTURE.md) - every other outstanding, rotating, active occurrence is
/// therefore eligible to move, regardless of how it came to be assigned.
/// </para>
/// <para>
/// <b>How the target is computed.</b> Purely from capacity, never from role names: each active
/// member's target share of the total movable minutes is their
/// <see cref="WeeklyTimeBudget.TotalWeeklyMinutes"/> divided by the household's combined total.
/// A two-person household with a 7:13 capacity split converges on a 35%/65% split on its own,
/// without this method - or <see cref="RotationPicker"/> - ever comparing a role name.
/// </para>
/// <para>
/// <b>How moves are chosen.</b> A single deterministic pass over the movable occurrences, sorted
/// by (date, definition, id). At each occurrence, the pool of legal candidates is exactly
/// <see cref="RotationPicker.EligibleMembers"/> intersected with
/// <see cref="RotationPicker.HasRoomToday"/> (falling back to eligibility alone if literally
/// nobody has room, so an occurrence never ends up unassigned) - the identical rules the live
/// picker already enforces, so a reassignment can never land on someone who could not have been
/// picked in the first place. The current owner is kept unless some OTHER candidate in the pool
/// has a strictly lower running ratio (assigned-so-far ÷ target) - ties, including "current
/// owner is already the lowest", favor keeping, which is what makes the whole pass minimal-change
/// by construction: a move only ever happens when it strictly improves the balance. This also
/// makes the result deterministic (fixed order, fixed tie-breaks) and idempotent (a second run
/// starts from the first run's own output, at which point no candidate has a strictly better
/// ratio left to offer - see <c>RebalanceTaskAssignmentsTests</c> for the argument in full).
/// </para>
/// <para>
/// <b>The daily cap.</b> "Room today" is judged against each occurrence's OWN scheduled date,
/// using a running per-date tally seeded from every OTHER already-outstanding occurrence on that
/// date (fixed and movable alike, from the same household-wide fetch) plus whatever this pass
/// has already committed for that date - so a rebalance can never quietly overbook a member's
/// real day, the same invariant <see cref="EnsureOccurrencesGenerated"/> already upholds for new
/// occurrences.
/// </para>
/// <para>
/// <b>Persistence and notification.</b> Every reassignment is applied to already-tracked
/// entities in memory; <see cref="ITaskOccurrenceRepository.UpdateAsync"/> is called exactly
/// once at the end (its current implementation flushes every pending change for the whole unit
/// of work in one call) - so the whole batch commits together or not at all, and nothing is left
/// half-migrated on failure. <see cref="IHouseholdNotifier.NotifyOccurrencesChangedAsync"/> is
/// likewise called at most once, and only when at least one assignment actually changed.
/// </para>
/// <para>
/// <b>Never touches time credit.</b> "Tid i förväg" (<c>MemberTimeCredit</c>, see
/// docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg")
/// only ever influences a NEW occurrence's rotation pick (<see cref="RotationPicker"/>, via
/// <see cref="EnsureOccurrencesGenerated"/>). This use case moves ALREADY-assigned work between
/// members - credit has nothing to say about that, and nothing here reads or writes a credit
/// balance.
/// </para>
/// </remarks>
public sealed class RebalanceTaskAssignments
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IMemberDayOffRepository _daysOff;
    private readonly IHouseholdNotifier _notifier;

    public RebalanceTaskAssignments(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        IMemberDayOffRepository daysOff,
        IHouseholdNotifier notifier)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
        _daysOff = daysOff;
        _notifier = notifier;
    }

    /// <summary>
    /// Rebalances the household's outstanding rotating work, or returns <c>null</c> if it does
    /// not exist. Returns how many occurrences actually changed owner.
    /// </summary>
    /// <param name="today">
    /// Used only to decide which occurrences are movable at all (see the "kvarlämnat" filter
    /// below) - an occurrence already overdue as of <paramref name="today"/> is excluded before
    /// any ratio math runs. The server's own clock is fine here (unlike most "what does today
    /// mean" call sites, see CLAUDE.md §5): a client a day off from the server only changes
    /// whether TODAY's own occurrences count as "due today" or "overdue by one day" for
    /// movability purposes, which is harmless either way.
    /// </param>
    public async Task<int?> HandleAsync(Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        var definitionsById = (await _definitions.ListByHouseholdAsync(householdId, cancellationToken))
            .ToDictionary(definition => definition.Id);

        var allOutstanding = await _occurrences.ListOutstandingByHouseholdAsync(householdId, cancellationToken);

        // Only occurrences whose definition still exists and is active are relevant at all - an
        // occurrence under an archived definition is left untouched, per docs/ARCHITECTURE.md.
        var relevant = allOutstanding
            .Where(occurrence => definitionsById.TryGetValue(occurrence.TaskDefinitionId, out var definition) && definition.IsActive)
            .ToList();

        // Kvarlämnat stannar hos den som lämnade det - en försenad uppgift är aldrig
        // flyttbar, oavsett kapacitet. Se docs/ARCHITECTURE.md.
        var movable = relevant
            .Where(occurrence => definitionsById[occurrence.TaskDefinitionId].HasRotatingResponsibility
                && occurrence.OriginalScheduledDate >= today)
            .OrderBy(occurrence => occurrence.ScheduledDate)
            .ThenBy(occurrence => occurrence.TaskDefinitionId)
            .ThenBy(occurrence => occurrence.Id)
            .ToList();

        if (movable.Count == 0)
        {
            return 0;
        }

        // A candidate is never eligible for a date they are off on - see RotationPicker, whose
        // EligibleMembers this reuses below. Loaded once, for exactly the date span this run
        // could possibly touch.
        var daysOffInRange = await _daysOff.ListForHouseholdAsync(
            householdId, movable[0].ScheduledDate, movable[^1].ScheduledDate, cancellationToken);
        var daysOff = daysOffInRange.Select(dayOff => (dayOff.MemberId, dayOff.Date)).ToHashSet();

        var fixedOccurrences = relevant.Except(movable);

        // Seeded from every fixed occurrence's own date/member - the daily cap below judges a
        // candidate's real day, not just their share of the movable pool.
        var dateTally = fixedOccurrences
            .Where(occurrence => occurrence.AssignedMemberId is not null)
            .GroupBy(occurrence => (occurrence.ScheduledDate, MemberId: occurrence.AssignedMemberId!.Value))
            .ToDictionary(group => group.Key, group => group.Sum(occurrence => occurrence.EstimatedMinutes));

        var activeMembers = household.Members.Where(member => member.IsActive).ToList();
        var totalCapacity = activeMembers.Sum(member => member.WeeklyTimeBudget.TotalWeeklyMinutes);
        var totalMovableMinutes = movable.Sum(occurrence => occurrence.EstimatedMinutes);

        var targetByMember = activeMembers.ToDictionary(
            member => member.Id,
            member => totalCapacity > 0
                ? totalMovableMinutes * ((double)member.WeeklyTimeBudget.TotalWeeklyMinutes / totalCapacity)
                : totalMovableMinutes / (double)activeMembers.Count);

        var runningByMember = new Dictionary<Guid, int>();
        var changedCount = 0;

        foreach (var occurrence in movable)
        {
            var definition = definitionsById[occurrence.TaskDefinitionId];
            var date = occurrence.ScheduledDate;

            var eligible = RotationPicker.EligibleMembers(household, definition, date, daysOff);
            var withRoom = eligible
                .Where(member => HasRoomForOccurrenceToday(member, date, dateTally, occurrence))
                .ToList();
            var pool = withRoom.Count > 0 ? withRoom : eligible;

            if (pool.Count == 0)
            {
                // No active, eligible member at all (e.g. every member paused on this date) -
                // nothing legal to assign this to; leave it exactly as it is.
                continue;
            }

            var currentOwnerId = occurrence.AssignedMemberId;
            var currentOwnerInPool = currentOwnerId is { } ownerId ? pool.FirstOrDefault(m => m.Id == ownerId) : null;

            HouseholdMember chosen;

            if (currentOwnerInPool is not null)
            {
                var currentRatio = RunningRatio(currentOwnerInPool, runningByMember, targetByMember);
                var best = pool
                    .OrderBy(member => RunningRatio(member, runningByMember, targetByMember))
                    .ThenBy(member => member.Id == definition.DefaultResponsibleMemberId ? 0 : 1)
                    .ThenBy(member => member.CreatedAt)
                    .ThenBy(member => member.Id)
                    .First();

                // Keep the current owner unless someone else in the pool is STRICTLY better off
                // - a tie (including "current owner already has the lowest ratio") favors
                // keeping, which is what minimizes how many occurrences actually move.
                chosen = RunningRatio(best, runningByMember, targetByMember) < currentRatio ? best : currentOwnerInPool;
            }
            else
            {
                // No current owner, or the current owner is no longer a legal candidate for
                // this occurrence (e.g. now paused, or a definition that became adults-only) -
                // it must move regardless of ratio, same as RotationPicker would decide fresh.
                chosen = pool
                    .OrderBy(member => RunningRatio(member, runningByMember, targetByMember))
                    .ThenBy(member => member.Id == definition.DefaultResponsibleMemberId ? 0 : 1)
                    .ThenBy(member => member.CreatedAt)
                    .ThenBy(member => member.Id)
                    .First();
            }

            if (chosen.Id != currentOwnerId)
            {
                occurrence.AssignTo(chosen.Id);
                changedCount++;
            }

            runningByMember[chosen.Id] = runningByMember.GetValueOrDefault(chosen.Id) + occurrence.EstimatedMinutes;
            var dateKey = (date, chosen.Id);
            dateTally[dateKey] = dateTally.GetValueOrDefault(dateKey) + occurrence.EstimatedMinutes;
        }

        if (changedCount > 0)
        {
            // The current implementation flushes every pending change for the whole unit of
            // work in one call, regardless of which occurrence is passed - see
            // TaskOccurrenceRepository.UpdateAsync - so this one call commits every
            // reassignment made above atomically, or none of them.
            await _occurrences.UpdateAsync(movable[0], cancellationToken);
            await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);
        }

        return changedCount;
    }

    /// <summary>
    /// <paramref name="dateTally"/> holds each member's committed minutes on
    /// <paramref name="date"/> from every fixed occurrence (seeded once, up front) plus every
    /// movable occurrence already decided earlier in this same pass - it never yet includes
    /// <paramref name="occurrence"/> itself, since that is exactly what is being decided now.
    /// </summary>
    private static bool HasRoomForOccurrenceToday(
        HouseholdMember member,
        DateOnly date,
        IReadOnlyDictionary<(DateOnly Date, Guid MemberId), int> dateTally,
        TaskOccurrence occurrence)
    {
        var alreadyOnDate = dateTally.GetValueOrDefault((date, member.Id));

        return RotationPicker.HasRoomToday(
            member, date, new Dictionary<Guid, int> { [member.Id] = alreadyOnDate }, occurrence.EstimatedMinutes);
    }

    private static double RunningRatio(
        HouseholdMember member, IReadOnlyDictionary<Guid, int> runningByMember, IReadOnlyDictionary<Guid, double> targetByMember)
    {
        var running = runningByMember.GetValueOrDefault(member.Id);
        var target = targetByMember.GetValueOrDefault(member.Id);

        return target > 0 ? running / target : running;
    }
}

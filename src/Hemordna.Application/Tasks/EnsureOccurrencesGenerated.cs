using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Generates the occurrences a recurring task definition owes, up to and including
/// <c>today</c>. This is the resolution of the open question in docs/ARCHITECTURE.md §10 -
/// generation happens on demand, called from <see cref="Planning.GetDailyPlan"/>, rather than
/// from a scheduled background job. Nothing else needs a hosted-service or queue
/// infrastructure yet, and "on demand" cannot silently generate work while no one is looking.
/// </summary>
public sealed class EnsureOccurrencesGenerated
{
    /// <summary>
    /// Safety bound on how many missed occurrences one definition can catch up on in a single
    /// call. A household that opens the app after months away should not get a runaway backlog.
    /// </summary>
    private const int MaxCatchUpPerDefinition = 366;

    /// <summary>
    /// How far back days off and time credit are read for this run - see docs/ARCHITECTURE.md
    /// "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg". A day off older than
    /// this is not looked up at all (an occurrence catching up on a slot that far in the past
    /// would be an extreme edge case already bounded by <see cref="MaxCatchUpPerDefinition"/>,
    /// and a day off is inherently a near-term thing - it can only ever be SET up to 7 days
    /// ahead, see <c>MemberDayOff.Create</c>). Credit older than this is likewise ignored by
    /// <c>MemberTimeCredit.BalanceOf</c>'s caller here, for the same "near-term, not a growing
    /// history" reasoning that keeps it out of the UI too (CLAUDE.md §12).
    /// </summary>
    private const int LookbackDays = 60;

    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IMemberDayOffRepository _daysOff;
    private readonly IMemberTimeCreditRepository _credits;
    private readonly TimeProvider _timeProvider;

    public EnsureOccurrencesGenerated(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        ITaskAssignmentRepository assignments,
        IMemberDayOffRepository daysOff,
        IMemberTimeCreditRepository credits,
        TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
        _assignments = assignments;
        _daysOff = daysOff;
        _credits = credits;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return;
        }

        var definitions = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);

        // A mutable snapshot, updated in place as each rotating pick is made below - see
        // RotationPicker's remarks on why this must reflect the whole batch, not just what is
        // already in the database when this method started.
        var assignedMinutesByMember = new Dictionary<Guid, int>(
            await _assignments.GetAssignedMinutesByMemberAsync(householdId, cancellationToken));

        // Same idea, scoped to one calendar date at a time and loaded lazily, one date at a
        // time, as generation actually reaches it - see RotationPicker's daily-cap remarks.
        var assignedMinutesByDate = new Dictionary<DateOnly, Dictionary<Guid, int>>();

        var lookbackStart = today.AddDays(-LookbackDays);

        var daysOffInRange = await _daysOff.ListForHouseholdAsync(householdId, lookbackStart, today, cancellationToken);
        var daysOff = daysOffInRange.Select(dayOff => (dayOff.MemberId, dayOff.Date)).ToHashSet();

        // Also updated in place as credit is consumed below (see RotationPicker's "tid i
        // förväg" remarks) - so a second occurrence generated in the same batch sees the FIRST
        // one's consumption, not a stale balance.
        var creditMinutes = new Dictionary<Guid, int>();

        foreach (var member in household.Members.Where(member => member.IsActive))
        {
            var entries = await _credits.ListForMemberAsync(householdId, member.Id, lookbackStart, today, cancellationToken);
            creditMinutes[member.Id] = MemberTimeCredit.BalanceOf(entries, member.WeeklyTimeBudget.TotalWeeklyMinutes);
        }

        foreach (var definition in definitions)
        {
            if (!definition.IsActive)
            {
                continue;
            }

            if (definition.Recurrence is { } recurrence)
            {
                await GenerateOnScheduleAsync(
                    household, definition, recurrence, today, assignedMinutesByMember, assignedMinutesByDate,
                    daysOff, creditMinutes, cancellationToken);
            }
            else if (definition.StaleAfterDays is { } staleAfterDays)
            {
                await GenerateIfStaleAsync(
                    household, definition, staleAfterDays, today, assignedMinutesByMember, assignedMinutesByDate,
                    daysOff, creditMinutes, cancellationToken);
            }
        }
    }

    private async Task GenerateOnScheduleAsync(
        Household household,
        TaskDefinition definition,
        RecurrenceRule recurrence,
        DateOnly today,
        Dictionary<Guid, int> assignedMinutesByMember,
        Dictionary<DateOnly, Dictionary<Guid, int>> assignedMinutesByDate,
        HashSet<(Guid MemberId, DateOnly Date)> daysOff,
        Dictionary<Guid, int> creditMinutes,
        CancellationToken cancellationToken)
    {
        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(
            household.Id, definition.Id, cancellationToken);

        var next = recurrence.NextOnOrAfter(lastDate?.AddDays(1) ?? recurrence.StartDate);

        // Counts skipped-for-pause slots too, not just generated ones - otherwise a household
        // paused for longer than this bound would never advance past the pause window at all.
        var iterations = 0;

        while (next <= today && iterations < MaxCatchUpPerDefinition)
        {
            if (!IsSkippedForPause(household, definition, next)
                // An occurrence that already sits on this exact date - typically one moved there
                // by RebalanceSchedule re-anchoring the definition's recurrence after it already
                // had an outstanding occurrence - already covers this slot. Without this check,
                // the cursor above (based on the OLD occurrence's immutable
                // OriginalScheduledDate, not where it was moved to) would not know that, and
                // this would generate a second, genuinely duplicate occurrence for the same date.
                && !await _occurrences.HasOutstandingOnDateAsync(household.Id, definition.Id, next, cancellationToken))
            {
                await ScheduleGeneratedOccurrenceAsync(
                    household, definition, next, today, assignedMinutesByMember, assignedMinutesByDate,
                    daysOff, creditMinutes, cancellationToken);
            }

            iterations++;
            next = recurrence.NextOnOrAfter(next.AddDays(1));
        }
    }

    /// <summary>
    /// Unlike calendar recurrence, "as needed" has no missed slots to catch up on - it only
    /// ever asks "is it due right now?", so at most one occurrence is generated per call.
    /// </summary>
    private async Task GenerateIfStaleAsync(
        Household household,
        TaskDefinition definition,
        int staleAfterDays,
        DateOnly today,
        Dictionary<Guid, int> assignedMinutesByMember,
        Dictionary<DateOnly, Dictionary<Guid, int>> assignedMinutesByDate,
        HashSet<(Guid MemberId, DateOnly Date)> daysOff,
        Dictionary<Guid, int> creditMinutes,
        CancellationToken cancellationToken)
    {
        if (await _occurrences.HasOutstandingAsync(household.Id, definition.Id, cancellationToken))
        {
            return;
        }

        var lastCompletedAt = await _occurrences.FindMostRecentCompletedAtAsync(
            household.Id, definition.Id, cancellationToken);

        var since = lastCompletedAt is { } completedAt
            ? DateOnly.FromDateTime(completedAt.UtcDateTime)
            : DateOnly.FromDateTime(definition.CreatedAt.UtcDateTime);

        if (since.AddDays(staleAfterDays) > today)
        {
            return;
        }

        if (IsSkippedForPause(household, definition, today))
        {
            // Stays "due" with nothing recorded, so it is simply asked again next call - unlike
            // calendar recurrence there is no slot to lose by waiting for the pause to lift.
            return;
        }

        await ScheduleGeneratedOccurrenceAsync(
            household, definition, today, today, assignedMinutesByMember, assignedMinutesByDate,
            daysOff, creditMinutes, cancellationToken);
    }

    /// <summary>
    /// True when nothing should be generated for <paramref name="definition"/> on
    /// <paramref name="date"/> because either the whole household is paused, or the task is a
    /// fixed (non-rotating) one owned by a member who is individually paused. A rotating task's
    /// paused members are instead simply excluded from <see cref="RotationPicker"/>'s candidates
    /// for that date - the task still needs doing, just not by them.
    /// </summary>
    private static bool IsSkippedForPause(Household household, TaskDefinition definition, DateOnly date)
    {
        if (household.IsPausedOn(date))
        {
            return true;
        }

        if (definition.HasRotatingResponsibility)
        {
            return false;
        }

        var owner = definition.DefaultResponsibleMemberId is { } ownerId
            ? household.Members.FirstOrDefault(member => member.Id == ownerId)
            : null;

        return owner is not null && owner.IsPausedOn(date);
    }

    private async Task ScheduleGeneratedOccurrenceAsync(
        Household household,
        TaskDefinition definition,
        DateOnly date,
        DateOnly today,
        Dictionary<Guid, int> assignedMinutesByMember,
        Dictionary<DateOnly, Dictionary<Guid, int>> assignedMinutesByDate,
        HashSet<(Guid MemberId, DateOnly Date)> daysOff,
        Dictionary<Guid, int> creditMinutes,
        CancellationToken cancellationToken)
    {
        var occurrence = definition.ScheduleFor(date, _timeProvider.GetUtcNow());
        Guid? memberId = null;

        if (definition.HasRotatingResponsibility)
        {
            var assignedMinutesOnDate = await GetOrLoadAssignedMinutesOnDateAsync(
                household.Id, date, assignedMinutesByDate, cancellationToken);

            var pick = RotationPicker.PickNext(
                household, definition, assignedMinutesByMember, assignedMinutesOnDate, date, daysOff, creditMinutes);

            if (pick is { } result)
            {
                memberId = result.MemberId;

                await _assignments.AddAsync(
                    TaskAssignment.Create(
                        household.Id, definition.Id, result.MemberId, date, _timeProvider.GetUtcNow(),
                        definition.EstimatedMinutes),
                    cancellationToken);

                assignedMinutesByMember[result.MemberId] =
                    assignedMinutesByMember.GetValueOrDefault(result.MemberId) + definition.EstimatedMinutes;
                assignedMinutesOnDate[result.MemberId] =
                    assignedMinutesOnDate.GetValueOrDefault(result.MemberId) + definition.EstimatedMinutes;

                // "Tid i förväg" actually changed who got this occurrence - record it as spent,
                // and update the running balance so a second occurrence generated later in this
                // same batch sees the already-reduced amount, not what the ledger said at the
                // start of the whole run.
                if (result.ConsumedFromMemberId is { } consumedFrom && result.ConsumedMinutes > 0)
                {
                    await _credits.AddAsync(
                        MemberTimeCredit.Consumed(
                            household.Id, consumedFrom, today, TimeCreditReason.RotationSkipped,
                            result.ConsumedMinutes, occurrence.Id),
                        cancellationToken);

                    creditMinutes[consumedFrom] = creditMinutes.GetValueOrDefault(consumedFrom) - result.ConsumedMinutes;
                }
            }
        }
        else if (occurrence.AssignedMemberId is { } fixedOwnerId && daysOff.Contains((fixedOwnerId, date)))
        {
            // A fixed task's owner is off on the day it would normally land on - it stays
            // theirs (never anyone else's, no credit involved), just pushed to their own next
            // available day, same 14-day search SetMemberDayOff's own DeferAll uses. If nothing
            // deferrable turns up within that window, or the task itself cannot be deferred at
            // all, it is left exactly where it is rather than left unscheduled or thrown away -
            // this runs silently in the background, not as a direct request someone is waiting
            // on an answer to.
            if (occurrence.CanBeDeferred
                && FindNextAvailableDate(fixedOwnerId, date, daysOff, maxDaysAhead: 14) is { } nextAvailable)
            {
                occurrence.DeferTo(nextAvailable);
            }
        }

        if (memberId is { } finalMemberId)
        {
            occurrence.AssignTo(finalMemberId);
        }

        await _occurrences.AddAsync(occurrence, cancellationToken);
    }

    private static DateOnly? FindNextAvailableDate(
        Guid memberId, DateOnly date, HashSet<(Guid MemberId, DateOnly Date)> daysOff, int maxDaysAhead)
    {
        for (var offset = 1; offset <= maxDaysAhead; offset++)
        {
            var candidate = date.AddDays(offset);

            if (!daysOff.Contains((memberId, candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<Dictionary<Guid, int>> GetOrLoadAssignedMinutesOnDateAsync(
        Guid householdId,
        DateOnly date,
        Dictionary<DateOnly, Dictionary<Guid, int>> assignedMinutesByDate,
        CancellationToken cancellationToken)
    {
        if (assignedMinutesByDate.TryGetValue(date, out var forDate))
        {
            return forDate;
        }

        forDate = new Dictionary<Guid, int>(
            await _assignments.GetAssignedMinutesByMemberOnDateAsync(householdId, date, cancellationToken));
        assignedMinutesByDate[date] = forDate;

        return forDate;
    }
}

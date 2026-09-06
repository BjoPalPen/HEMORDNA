using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Re-anchors a household's already-created recurring tasks so they spread across the week
/// (or month) instead of clustering on whichever day they happened to be created - the
/// one-time fix for a household set up before the client's room-creation flow spread new
/// tasks across days itself, or one whose rooms were each set up in their own sitting.
/// </summary>
/// <remarks>
/// Tasks that share an <see cref="TaskDefinition.AreaId"/> (the same room) are re-anchored
/// together, to the same day - the point is to spread *rooms* across the week, not to scatter
/// one room's own tasks away from each other. A task with no area (an "Övrigt" chore) has
/// nothing to stay grouped with, so each becomes its own group.
/// <para>
/// Two things move, not just one: <see cref="TaskDefinition.Recurrence"/>, for occurrences not
/// generated yet, AND any already-generated, still-outstanding occurrence due today or earlier
/// - otherwise a household whose first-ever occurrences were all generated before this feature
/// existed (or before it was last run) would stay clustered forever, since a defintion's own
/// recurrence rule only governs occurrences generated <em>after</em> it changes. An occurrence
/// already due in the future, or already completed/skipped, is left alone - see
/// <see cref="TaskOccurrence.DeferTo"/>.
/// </para>
/// </remarks>
public sealed class RebalanceSchedule
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly TimeProvider _timeProvider;

    public RebalanceSchedule(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Re-anchors the household's recurring tasks, or returns <c>null</c> if the household does
    /// not exist. Returns how many task definitions were touched - either their own future
    /// recurrence changed, an already-generated occurrence of theirs moved, or both.
    /// </summary>
    public async Task<int?> HandleAsync(Guid householdId, CancellationToken cancellationToken)
    {
        if (await _households.FindByIdAsync(householdId, cancellationToken) is null)
        {
            return null;
        }

        // Read-only planning pass: ListByHouseholdAsync returns untracked snapshots (cheap for
        // the common read-only callers, like the task list endpoint), so anything that actually
        // needs a change is re-fetched below through FindByIdAsync instead, which returns a
        // tracked instance SaveChanges can actually persist.
        var definitions = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var groups = definitions
            .Where(definition => definition.IsActive && definition.Recurrence is not null)
            .GroupBy(definition => definition.AreaId)
            .SelectMany(byArea => byArea.Key is null
                // No room to stay grouped with - each is its own group.
                ? byArea.Select(definition => new[] { definition })
                : [byArea.OrderBy(definition => definition.Id).ToArray()])
            .OrderBy(group => group.Min(definition => definition.Id))
            .ToList();

        var touchedDefinitionIds = new HashSet<Guid>();

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            foreach (var definition in groups[groupIndex])
            {
                if (Reanchor(definition.Recurrence!, today, groupIndex) is not { } reanchored)
                {
                    continue;
                }

                if (!reanchored.Equals(definition.Recurrence)
                    && await _definitions.FindByIdAsync(householdId, definition.Id, cancellationToken) is { } tracked)
                {
                    tracked.SetRecurrence(reanchored);
                    await _definitions.UpdateAsync(tracked, cancellationToken);
                    touchedDefinitionIds.Add(definition.Id);
                }

                if (await RescheduleBacklogAsync(householdId, definition.Id, reanchored.StartDate, today, cancellationToken))
                {
                    touchedDefinitionIds.Add(definition.Id);
                }
            }
        }

        return touchedDefinitionIds.Count;
    }

    /// <summary>
    /// Moves this definition's already-generated, outstanding occurrences due on or before
    /// <paramref name="today"/> to <paramref name="targetDate"/> - the same date its recurrence
    /// now anchors to. Returns whether anything actually moved.
    /// </summary>
    private async Task<bool> RescheduleBacklogAsync(
        Guid householdId, Guid definitionId, DateOnly targetDate, DateOnly today, CancellationToken cancellationToken)
    {
        var backlog = await _occurrences.ListOutstandingOnOrBeforeAsync(
            householdId, definitionId, today, cancellationToken);
        var rescheduledAny = false;

        foreach (var occurrence in backlog)
        {
            // DeferTo only accepts a strictly later date (and only when CanBeDeferred) - an
            // occurrence already sitting exactly on the target, or one that cannot be pushed at
            // all, is left as-is rather than attempted and failing.
            if (!occurrence.CanBeDeferred || targetDate <= occurrence.ScheduledDate)
            {
                continue;
            }

            occurrence.DeferTo(targetDate);
            await _occurrences.UpdateAsync(occurrence, cancellationToken);
            rescheduledAny = true;
        }

        return rescheduledAny;
    }

    /// <summary>
    /// A new rule with the same frequency and interval, anchored to spread this group across
    /// the week/month/cycle - or <c>null</c> if the rule has no anchor worth moving (daily, or
    /// a specific "third Tuesday"-style rule nobody asked to have shuffled).
    /// </summary>
    private static RecurrenceRule? Reanchor(RecurrenceRule rule, DateOnly today, int groupIndex)
    {
        var shiftedAnchor = today.AddDays(groupIndex);

        return rule switch
        {
            { Frequency: RecurrenceFrequency.Weekly } => RecurrenceRule.Weekly(today, shiftedAnchor.DayOfWeek, rule.Interval),
            { Frequency: RecurrenceFrequency.Monthly, MonthlyWeek: null } => RecurrenceRule.Monthly(shiftedAnchor, rule.Interval),
            { Frequency: RecurrenceFrequency.Daily, Interval: > 1 } => RecurrenceRule.Daily(shiftedAnchor, rule.Interval),
            _ => null
        };
    }
}

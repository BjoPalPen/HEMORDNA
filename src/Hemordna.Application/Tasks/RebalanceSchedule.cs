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
/// nothing to stay grouped with, so each becomes its own group. Only <see
/// cref="TaskDefinition.Recurrence"/> is touched; already-generated, outstanding occurrences
/// keep their original date - see <see cref="TaskDefinition.SetRecurrence"/>.
/// </remarks>
public sealed class RebalanceSchedule
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly TimeProvider _timeProvider;

    public RebalanceSchedule(
        IHouseholdRepository households, ITaskDefinitionRepository definitions, TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Re-anchors the household's recurring tasks, or returns <c>null</c> if the household does
    /// not exist. Returns how many task definitions were changed.
    /// </summary>
    public async Task<int?> HandleAsync(Guid householdId, CancellationToken cancellationToken)
    {
        if (await _households.FindByIdAsync(householdId, cancellationToken) is null)
        {
            return null;
        }

        // Read-only planning pass: ListByHouseholdAsync returns untracked snapshots (cheap for
        // the common read-only callers, like the task list endpoint), so any definition that
        // actually needs a new anchor is re-fetched below through FindByIdAsync instead, which
        // returns a tracked instance SaveChanges can actually persist.
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

        var changed = 0;

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            foreach (var definition in groups[groupIndex])
            {
                if (Reanchor(definition.Recurrence!, today, groupIndex) is not { } reanchored
                    || reanchored.Equals(definition.Recurrence))
                {
                    continue;
                }

                var tracked = await _definitions.FindByIdAsync(householdId, definition.Id, cancellationToken);

                if (tracked is null)
                {
                    continue;
                }

                tracked.SetRecurrence(reanchored);
                await _definitions.UpdateAsync(tracked, cancellationToken);
                changed++;
            }
        }

        return changed;
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

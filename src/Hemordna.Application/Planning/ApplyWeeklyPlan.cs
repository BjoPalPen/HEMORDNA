using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// "Använd" - writes a weekly placement plan by re-anchoring each affected task's own
/// <see cref="TaskDefinition.Recurrence"/> to its newly chosen weekday.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gäller framåt - orört bakåt.</b> Only <see cref="TaskDefinition.Recurrence"/> changes.
/// Already-generated, outstanding occurrences are never touched, deferred or re-anchored -
/// unlike <see cref="RebalanceSchedule"/>, which deliberately DOES move outstanding backlog.
/// Björn has explicitly asked that using a plan never rewrites work already laid out on someone's
/// day - see docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen".
/// </para>
/// <para>
/// <b>A locked task is never moved.</b> A task with <see cref="TaskDefinition.PreferredWeekday"/>
/// set is placed by <see cref="WeeklyPlacementBuilder"/> on that exact day every time, so this
/// use case skips it explicitly - see docs/ARCHITECTURE.md "Beslut: Alltid på en viss veckodag".
/// </para>
/// <para>
/// <b>A moved visit locks.</b> Björns beslut: choosing a day for a visit in the preview (Björns
/// krav, "Planera veckan går att ändra") is not a one-off suggestion override - applying it sets
/// <see cref="TaskDefinition.PreferredWeekday"/> on every task in that visit, via
/// <see cref="RecurrenceReanchoring.LockToWeekday"/> (the exact same technique
/// <c>SetTaskPreferredWeekday</c> uses for a manual lock), so the NEXT "Planera veckan" does not
/// quietly move it back. Moving an already-locked visit changes the lock to the new day.
/// </para>
/// <para>
/// <b>No duplicate, no skipped period.</b> Every new <see cref="RecurrenceRule"/> is anchored
/// from <paramref name="today" /> forward (via <see cref="RecurrenceRule.Weekly"/> or
/// <see cref="RecurrenceRule.MonthlyOnWeekday"/>, both of which normalise their own anchor to the
/// first matching date ON OR AFTER the date passed in) - never from the OLD cursor
/// (<c>FindMostRecentOriginalDateAsync</c>, which <see cref="EnsureOccurrencesGenerated"/> reads
/// next time it runs). Because <see cref="EnsureOccurrencesGenerated"/> never generates ahead of
/// today in the first place, no occurrence can ever exist for a date later than today - so an
/// anchor rooted at today can never collide with, duplicate, or leave a gap before, anything
/// already on the calendar. This is the exact same anchoring technique
/// <see cref="RebalanceSchedule"/> already relies on for its own re-anchoring.
/// </para>
/// </remarks>
public sealed class ApplyWeeklyPlan
{
    /// <summary>Spreads multiple monthly tasks landing on the same weekday across different
    /// weeks of the month, instead of all landing on (say) every "third Tuesday".</summary>
    private static readonly WeekOfMonth[] MonthlyWeekRotation =
        [WeekOfMonth.First, WeekOfMonth.Second, WeekOfMonth.Third, WeekOfMonth.Fourth];

    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly WeeklyPlacementPlanner _planner = new();

    public ApplyWeeklyPlan(IHouseholdRepository households, ITaskDefinitionRepository definitions)
    {
        _households = households;
        _definitions = definitions;
    }

    private static readonly IReadOnlyDictionary<Guid, DayOfWeek> NoMoves = new Dictionary<Guid, DayOfWeek>();

    /// <summary>
    /// Applies the plan, or returns <c>null</c> when the household does not exist. Returns how
    /// many task definitions actually got a new recurrence anchor or a new lock (unchanged ones -
    /// already sitting on the day the plan would have chosen anyway - are left alone).
    /// <paramref name="moves"/> (visit key -> chosen weekday, see
    /// <see cref="PlaceableVisit.VisitKey"/>) is the household member's own edits to the
    /// suggestion - must be the SAME moves the preview that was shown was computed with, or the
    /// applied plan will not match what was previewed.
    /// </summary>
    public async Task<int?> HandleAsync(
        Guid householdId, DateOnly today, IReadOnlyDictionary<Guid, DayOfWeek>? moves, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        // Read-only planning pass - see RebalanceSchedule's own remark on why anything that
        // actually changes is re-fetched below through FindByIdAsync instead.
        var definitions = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);
        var byId = definitions.ToDictionary(definition => definition.Id);

        var request = WeeklyPlacementBuilder.Build(household, definitions, today);
        var effectiveMoves = moves ?? NoMoves;
        var visits = WeeklyPlacementBuilder.ApplyMoves(request.Visits, effectiveMoves);
        var result = _planner.Plan(request with { Visits = visits });

        // Monthly tasks are spread across weeks of the month by the order they were placed in -
        // deterministic and stable regardless of which weekday they land on, since the plan
        // itself is already a deterministic, ordered sequence of visits.
        var monthlyRotationIndexByDay = new Dictionary<DayOfWeek, int>();
        var changedCount = 0;

        foreach (var placement in result.PlacedVisits)
        {
            var isMoved = effectiveMoves.ContainsKey(placement.Visit.VisitKey);

            foreach (var taskDefinitionId in placement.Visit.TaskDefinitionIds)
            {
                if (!byId.TryGetValue(taskDefinitionId, out var definition) || definition.Recurrence is not { } current)
                {
                    continue;
                }

                if (isMoved)
                {
                    // Björns beslut: "ett flyttat besök låses" - every task in a moved visit gets
                    // TaskDefinition.PreferredWeekday, the same as a manual lock, via the shared
                    // RecurrenceReanchoring.LockToWeekday. Unlike the ordinary branch below, this
                    // runs even when the task was ALREADY locked - moving an already-locked visit
                    // changes the lock to the new day rather than being skipped.
                    var alreadyLockedHere = definition.PreferredWeekday == placement.Day && current.Weekday == placement.Day;

                    if (!alreadyLockedHere
                        && await _definitions.FindByIdAsync(householdId, taskDefinitionId, cancellationToken) is { } trackedMoved)
                    {
                        RecurrenceReanchoring.LockToWeekday(trackedMoved, today, placement.Day);
                        await _definitions.UpdateAsync(trackedMoved, cancellationToken);
                        changedCount++;
                    }

                    continue;
                }

                // A locked task (Björns krav, "Alltid på en viss veckodag") that was NOT moved
                // this round is never moved by this use case, full stop - the planner already
                // places it on its own PreferredWeekday (see WeeklyPlacementBuilder), so this
                // would be a no-op via HasMeaningfulChange anyway, but an explicit skip makes the
                // guarantee hold regardless of that coincidence.
                if (definition.PreferredWeekday is not null)
                {
                    continue;
                }

                var newRecurrence = RecurrenceReanchoring.ForWeekday(
                    current, today, placement.Day, () => NextMonthlyWeek(monthlyRotationIndexByDay, placement.Day));

                if (!RecurrenceReanchoring.HasMeaningfulChange(newRecurrence, current))
                {
                    continue;
                }

                if (await _definitions.FindByIdAsync(householdId, taskDefinitionId, cancellationToken) is { } tracked)
                {
                    tracked.SetRecurrence(newRecurrence);
                    await _definitions.UpdateAsync(tracked, cancellationToken);
                    changedCount++;
                }
            }
        }

        return changedCount;
    }

    private static WeekOfMonth NextMonthlyWeek(Dictionary<DayOfWeek, int> rotationIndexByDay, DayOfWeek day)
    {
        var index = rotationIndexByDay.GetValueOrDefault(day);
        rotationIndexByDay[day] = index + 1;
        return MonthlyWeekRotation[index % MonthlyWeekRotation.Length];
    }
}

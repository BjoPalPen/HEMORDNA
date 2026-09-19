using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// Builds a <see cref="WeeklyPlacementRequest"/> from a household's current, live state - shared
/// by <see cref="PreviewWeeklyPlan"/> and <see cref="ApplyWeeklyPlan"/> so the two always agree
/// on what "the current week" looks like.
/// </summary>
internal static class WeeklyPlacementBuilder
{
    /// <summary>
    /// <paramref name="today"/> decides which members currently count as active capacity (not
    /// paused right now) - a deliberate simplification for a WEEKLY planning aid: a pause that
    /// lifts mid-week is not modelled day-by-day here, unlike <see cref="Planning.DailyPlanner"/>'s
    /// own per-day precision.
    /// </summary>
    public static WeeklyPlacementRequest Build(Household household, IReadOnlyList<TaskDefinition> definitions, DateOnly today)
    {
        var activeMembers = household.Members.Where(member => member.IsActive && !member.IsPausedOn(today)).ToList();

        // Routine tasks take their place first - the same total comes off every weekday, since a
        // Routine task (daily, interval 1) recurs every day alike. See docs/ARCHITECTURE.md.
        var routineMinutes = definitions
            .Where(definition => definition.IsActive && VisitKindClassifier.Of(definition) == VisitKind.Routine)
            .Sum(definition => definition.EstimatedMinutes);

        var weekdays = Enum.GetValues<DayOfWeek>()
            .Select(day => new WeekdayCapacity(
                day,
                Math.Max(0, activeMembers.Sum(member => member.WeeklyTimeBudget.MinutesFor(day)) - routineMinutes),
                // No active member at all (an edge case - everyone paused) has no ceiling to
                // cap by; defaulting to Heavy avoids an artificial restriction nobody chose.
                activeMembers.Count > 0
                    ? activeMembers.Max(member => member.WeeklyEffortCeiling.CeilingFor(day))
                    : TaskEffort.Heavy))
            .ToList();

        var placeable = definitions
            .Where(definition => definition.IsActive
                && IsPlaceableCadence(definition.Recurrence)
                && VisitKindClassifier.Of(definition) != VisitKind.Routine)
            .GroupBy(definition => (definition.AreaId, Kind: VisitKindClassifier.Of(definition)))
            .SelectMany(group => group.Key.AreaId is null
                // "Övrigt" has no room to hold a visit together - each task is its own visit,
                // locked to its own PreferredWeekday if it has one.
                ? group.Select(definition => (Tasks: (IReadOnlyList<TaskDefinition>)[definition], LockedWeekday: definition.PreferredWeekday))
                : SplitByLock(group.ToArray()))
            .Select(entry => new PlaceableVisit(
                entry.Tasks[0].AreaId,
                entry.Tasks[0].AreaId is { } areaId ? household.Areas.FirstOrDefault(area => area.Id == areaId)?.Name : null,
                VisitKindClassifier.Of(entry.Tasks[0]),
                entry.Tasks.Sum(definition => definition.EstimatedMinutes),
                entry.Tasks.Max(definition => definition.Effort),
                [.. entry.Tasks.Select(definition => definition.Id)],
                entry.LockedWeekday))
            .ToList();

        return new WeeklyPlacementRequest(weekdays, placeable);
    }

    /// <summary>
    /// Splits one room's (AreaId, VisitKind) group into the visit(s) it actually becomes, given
    /// which of its tasks are locked to a weekday (Björns krav, "Alltid på en viss veckodag").
    /// </summary>
    /// <remarks>
    /// Zero or exactly one distinct locked weekday among the group's tasks: the room rule holds
    /// - the WHOLE group (locked and unlocked tasks together) stays one visit, carrying that
    /// single day as its <see cref="PlaceableVisit.LockedWeekday"/> (or null when none of them
    /// are locked). More than one distinct locked weekday: the room rule cannot hold everyone
    /// together any more - each locked task becomes its own single-task visit on its own day,
    /// and any remaining unlocked tasks form their own ordinary visit, placed by the greedy
    /// algorithm as usual.
    /// </remarks>
    private static IEnumerable<(IReadOnlyList<TaskDefinition> Tasks, DayOfWeek? LockedWeekday)> SplitByLock(
        IReadOnlyList<TaskDefinition> group)
    {
        var lockedDays = group
            .Where(definition => definition.PreferredWeekday is not null)
            .Select(definition => definition.PreferredWeekday!.Value)
            .Distinct()
            .ToList();

        if (lockedDays.Count <= 1)
        {
            yield return (group, lockedDays.Count == 1 ? lockedDays[0] : null);
            yield break;
        }

        foreach (var day in lockedDays)
        {
            yield return ([.. group.Where(definition => definition.PreferredWeekday == day)], day);
        }

        var unlocked = group.Where(definition => definition.PreferredWeekday is null).ToList();

        if (unlocked.Count > 0)
        {
            yield return (unlocked, null);
        }
    }

    /// <summary>
    /// The household's current remaining capacity per weekday - the same base computation as
    /// <see cref="Build"/>, further reduced by every EXISTING weekly/monthly task's minutes on
    /// its OWN current weekday. Used to place a brand-new task into the room already there is,
    /// without recomputing (and potentially moving) anyone else's placement - see
    /// <see cref="NewTaskWeekdayPlacement"/>.
    /// </summary>
    /// <remarks>
    /// A plain day-of-month monthly task (<see cref="RecurrenceRule.MonthlyWeek"/> null) has no
    /// single weekday of its own to charge against - it is left out of this reduction, a
    /// deliberate, minor simplification (see docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen").
    /// </remarks>
    public static IReadOnlyList<WeekdayCapacity> RemainingCapacity(
        Household household, IReadOnlyList<TaskDefinition> definitions, DateOnly today)
    {
        var baseCapacity = Build(household, definitions, today).Weekdays.ToDictionary(w => w.Day, w => w);

        var spentByDay = definitions
            .Where(definition => definition.IsActive
                && definition.Recurrence is { Weekday: { } }
                // Guards against a rule that was re-anchored to a weekday BEFORE this change
                // shipped and so happens to still carry one despite being sparse (Interval > 1) -
                // see docs/ARCHITECTURE.md "Beslut: Glesa regler lämnas i fred". No migration
                // rewrites those; this filter just stops charging them against a day here.
                && definition.Recurrence.IsWeeklyRhythm
                && VisitKindClassifier.Of(definition) != VisitKind.Routine)
            .GroupBy(definition => definition.Recurrence!.Weekday!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(definition => definition.EstimatedMinutes));

        // Deliberately NOT clamped at zero, unlike Build()'s own base computation - a household
        // with little or no time budget yet (the default for a brand-new one, before anyone
        // sets a weekly budget - see docs/PRODUCT.md §5) would otherwise see every weekday
        // clamp to the same 0 "remaining", erasing the very signal this method exists to
        // preserve: which day already has more already-placed work than another. The planner
        // itself already treats negative remaining minutes as a normal, orderable value - see
        // WeeklyPlacementPlanner's own "Overflow is allowed" remarks.
        return Enum.GetValues<DayOfWeek>()
            .Select(day => baseCapacity[day] with
            {
                AvailableMinutes = baseCapacity[day].AvailableMinutes - spentByDay.GetValueOrDefault(day)
            })
            .ToList();
    }

    /// <summary>
    /// Overlays the household member's own pending moves (Björns krav, "Planera veckan går att
    /// ändra") on top of an already-built visit list, at exactly the same layer as an existing
    /// <see cref="TaskDefinition.PreferredWeekday"/> lock: <see cref="PlaceableVisit.LockedWeekday"/>.
    /// <see cref="WeeklyPlacementPlanner"/> then places a moved visit first, unconditionally, the
    /// same as any other locked one - no second placement mechanism. A move overrides whatever
    /// lock a visit already carries; moving an already-locked visit changes the lock to the new
    /// day (see <c>ApplyWeeklyPlan</c>). Grouping itself (which tasks belong to which visit) is
    /// never affected - a move only changes WHERE a visit that already exists gets placed.
    /// </summary>
    public static IReadOnlyList<PlaceableVisit> ApplyMoves(
        IReadOnlyList<PlaceableVisit> visits, IReadOnlyDictionary<Guid, DayOfWeek> moves)
    {
        if (moves.Count == 0)
        {
            return visits;
        }

        return [.. visits.Select(visit => moves.TryGetValue(visit.VisitKey, out var day)
            ? visit with { LockedWeekday = day }
            : visit)];
    }

    /// <summary>
    /// Weekly and monthly tasks with Interval 1 get a weekday placement. Daily tasks (interval
    /// 2-3, e.g. "TwiceWeekly"/"EveryOtherDay" on the client) are spread by their own start date
    /// phase instead - a single weekday choice does not describe a task that lands on a different
    /// weekday every cycle. "Vid behov" (no <see cref="RecurrenceRule"/> at all) is never placed.
    /// Nor is a SPARSE Weekly/Monthly rule (Interval &gt; 1, e.g. "every 12 months") - its own
    /// StartDate carries WHICH month or phase is meant, which re-anchoring to a weekday would
    /// destroy; see <see cref="RecurrenceRule.IsWeeklyRhythm"/> and docs/ARCHITECTURE.md "Beslut:
    /// Glesa regler lämnas i fred". See "Beslut: Placeringsalgoritmen" for the rest of the
    /// reasoning.
    /// </summary>
    private static bool IsPlaceableCadence(RecurrenceRule? recurrence) => recurrence?.IsWeeklyRhythm == true;
}

using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// One visit - the tasks a household does together in one room on one occasion, e.g. "the
/// bathroom's weekly clean" - not yet anchored to a weekday. Tasks with no room ("Övrigt") are
/// never grouped together; each becomes its own single-task visit - see the use case that builds
/// these from a household's task definitions.
/// </summary>
/// <remarks>
/// Deliberately carries no cadence (weekly/monthly) of its own - a visit can mix tasks of both
/// (e.g. a bedroom's weekly vacuuming and its monthly skirting-board wipe are the same
/// RegularClean visit). Only the WEEKDAY is shared across a visit's tasks; each task keeps its
/// own frequency when its new <see cref="RecurrenceRule"/> is built after placement - see the
/// use case that applies a plan.
/// </remarks>
/// <param name="LockedWeekday">
/// Set when every task in this visit shares the same <see cref="TaskDefinition.PreferredWeekday"/>
/// (Björns krav, "Alltid på en viss veckodag") - the visit is placed on exactly this day,
/// unconditionally, instead of going through the greedy algorithm. Null for an ordinary,
/// freely-placed visit. See <see cref="WeeklyPlacementBuilder"/> for how a room's visit is split
/// when its tasks disagree on which day they are locked to.
/// </param>
public sealed record PlaceableVisit(
    Guid? AreaId,
    string? AreaName,
    VisitKind VisitKind,
    int Minutes,
    TaskEffort RequiredEffort,
    IReadOnlyList<Guid> TaskDefinitionIds,
    DayOfWeek? LockedWeekday = null);

/// <summary>The household's placeable capacity for one weekday - see
/// docs/ARCHITECTURE.md for how <paramref name="AvailableMinutes"/> and
/// <paramref name="MaxEffort"/> are computed from the household's current members.</summary>
public sealed record WeekdayCapacity(DayOfWeek Day, int AvailableMinutes, TaskEffort MaxEffort);

/// <summary>Input to <see cref="WeeklyPlacementPlanner"/>. <paramref name="Weekdays"/> must
/// contain exactly one entry per <see cref="DayOfWeek"/>.</summary>
public sealed record WeeklyPlacementRequest(
    IReadOnlyList<WeekdayCapacity> Weekdays,
    IReadOnlyList<PlaceableVisit> Visits);

/// <summary>Where one visit landed.</summary>
public sealed record PlacedVisit(PlaceableVisit Visit, DayOfWeek Day);

/// <summary>
/// The placement suggestion: every visit's chosen weekday, and each weekday's minutes before and
/// after placement (for the "Planera veckan" preview - see docs/ARCHITECTURE.md). Never a
/// per-person number - see the use case that renders this to the API and UI.
/// </summary>
public sealed record WeeklyPlacementResult(
    IReadOnlyList<PlacedVisit> PlacedVisits,
    IReadOnlyDictionary<DayOfWeek, int> MinutesBeforeByDay,
    IReadOnlyDictionary<DayOfWeek, int> MinutesAfterByDay);

/// <summary>
/// Decides which weekday each visit lands on - a pure, deterministic function with no
/// dependencies, no clock and no storage, same requirement as <see cref="DailyPlanner"/>: the
/// same request always produces the same plan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Greedy, not optimal.</b> Visits are placed one at a time, heaviest first
/// (<see cref="VisitKind.DeepClean"/> before <see cref="VisitKind.RegularClean"/>), then by
/// largest total minutes. Each visit goes to the ALLOWED weekday (one whose
/// <see cref="WeekdayCapacity.MaxEffort"/> is at least the visit's
/// <see cref="PlaceableVisit.RequiredEffort"/>) with the most minutes still remaining after
/// everything placed before it. A tie is broken by weekday, Monday first - stable and
/// deterministic regardless of the order visits arrive in the request.
/// </para>
/// <para>
/// <b>Fallback: no allowed weekday.</b> If no weekday's ceiling reaches the visit's required
/// effort at all (e.g. every member's Saturday ceiling is Medium but a visit needs Heavy), every
/// weekday becomes a candidate instead - the visit still needs a day, same "always assign, never
/// drop" fallback pattern <c>RotationPicker</c> already uses for <c>RequiresAdult</c> and the
/// effort ceiling.
/// </para>
/// <para>
/// <b>Overflow is allowed.</b> A weekday's remaining minutes can go negative once a visit is
/// placed there - this is a planning aid, not a hard capacity limiter.
/// </para>
/// <para>
/// <b>Locked visits go first, unconditionally.</b> A visit with
/// <see cref="PlaceableVisit.LockedWeekday"/> set is placed on exactly that day before anything
/// else, without regard to <see cref="WeekdayCapacity.MaxEffort"/> - the requirement wins,
/// `RotationPicker`'s own existing fallback settles WHO ends up doing it. Its minutes are
/// subtracted from that day's remaining capacity first, so the greedy pass below spreads the
/// REST of the week around it rather than in ignorance of it. See docs/ARCHITECTURE.md "Beslut:
/// Alltid på en viss veckodag".
/// </para>
/// </remarks>
public sealed class WeeklyPlacementPlanner
{
    /// <summary>Monday first, matching the tie-break rule - never <see cref="DayOfWeek"/>'s own
    /// underlying order, which starts at Sunday.</summary>
    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public WeeklyPlacementResult Plan(WeeklyPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Weekdays);
        ArgumentNullException.ThrowIfNull(request.Visits);

        if (request.Weekdays.Count != 7 || request.Weekdays.Select(w => w.Day).Distinct().Count() != 7)
        {
            throw new ArgumentException("Weekdays must contain exactly one entry per DayOfWeek.", nameof(request));
        }

        var maxEffortByDay = request.Weekdays.ToDictionary(w => w.Day, w => w.MaxEffort);
        var remaining = request.Weekdays.ToDictionary(w => w.Day, w => w.AvailableMinutes);
        var before = new Dictionary<DayOfWeek, int>(remaining);

        var placed = new List<PlacedVisit>(request.Visits.Count);

        // Locked visits first - placed on their own day unconditionally, and counted into
        // capacity, so the greedy pass below evens the rest of the week out AROUND them.
        var lockedVisits = request.Visits
            .Where(visit => visit.LockedWeekday is not null)
            .OrderBy(visit => Array.IndexOf(WeekOrder, visit.LockedWeekday!.Value))
            .ThenBy(visit => visit.TaskDefinitionIds.Count > 0 ? visit.TaskDefinitionIds.Min() : Guid.Empty);

        foreach (var visit in lockedVisits)
        {
            var day = visit.LockedWeekday!.Value;
            remaining[day] -= visit.Minutes;
            placed.Add(new PlacedVisit(visit, day));
        }

        var orderedVisits = request.Visits
            .Where(visit => visit.LockedWeekday is null)
            .OrderByDescending(visit => visit.VisitKind == VisitKind.DeepClean)
            .ThenByDescending(visit => visit.Minutes)
            .ThenBy(visit => visit.TaskDefinitionIds.Count > 0 ? visit.TaskDefinitionIds.Min() : Guid.Empty)
            .ToList();

        foreach (var visit in orderedVisits)
        {
            var allowed = WeekOrder.Where(day => maxEffortByDay[day] >= visit.RequiredEffort).ToList();
            var candidates = allowed.Count > 0 ? allowed : WeekOrder.ToList();

            var chosenDay = candidates
                .OrderByDescending(day => remaining[day])
                .ThenBy(day => Array.IndexOf(WeekOrder, day))
                .First();

            remaining[chosenDay] -= visit.Minutes;
            placed.Add(new PlacedVisit(visit, chosenDay));
        }

        return new WeeklyPlacementResult(placed, before, remaining);
    }
}

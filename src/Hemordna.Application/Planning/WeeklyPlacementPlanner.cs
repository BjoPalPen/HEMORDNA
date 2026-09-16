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
public sealed record PlaceableVisit(
    Guid? AreaId,
    string? AreaName,
    VisitKind VisitKind,
    int Minutes,
    TaskEffort RequiredEffort,
    IReadOnlyList<Guid> TaskDefinitionIds);

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

        var orderedVisits = request.Visits
            .OrderByDescending(visit => visit.VisitKind == VisitKind.DeepClean)
            .ThenByDescending(visit => visit.Minutes)
            .ThenBy(visit => visit.TaskDefinitionIds.Count > 0 ? visit.TaskDefinitionIds.Min() : Guid.Empty)
            .ToList();

        var placed = new List<PlacedVisit>(orderedVisits.Count);

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

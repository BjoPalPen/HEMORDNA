using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// Decides a brand-new task's weekday at CREATION time, using the same placement algorithm as
/// "Planera veckan" (<see cref="WeeklyPlacementPlanner"/>) - but scoped to just the new task, and
/// never moving anything already scheduled. See docs/ARCHITECTURE.md "Beslut:
/// Placeringsalgoritmen" for why this replaces the client's old naive "next weekday in turn"
/// spreading (<c>roomSpreadIndex</c> in <c>Rum.razor</c>/<c>RoomSheet.razor</c>).
/// </summary>
internal static class NewTaskWeekdayPlacement
{
    /// <summary>
    /// Chooses the weekday for a new task with the given room, visit kind, effort and minutes.
    /// </summary>
    /// <remarks>
    /// <b>Joins an existing visit first.</b> If an active task already shares this
    /// (<paramref name="areaId"/>, <paramref name="visitKind"/>) and has a weekday of its own,
    /// the new task simply joins it - a room's visit stays on one day, exactly as "Planera
    /// veckan" itself keeps a visit's tasks together. Only when no such visit exists yet does
    /// this run the greedy algorithm, against the household's CURRENT remaining capacity
    /// (<see cref="WeeklyPlacementBuilder.RemainingCapacity"/>) - so the new task fills a real
    /// gap without recomputing, or moving, anyone else's placement.
    /// </remarks>
    public static DayOfWeek Choose(
        Household household,
        IReadOnlyList<TaskDefinition> existingDefinitions,
        DateOnly today,
        Guid? areaId,
        VisitKind visitKind,
        TaskEffort effort,
        int estimatedMinutes)
    {
        var sibling = areaId is { } id
            ? existingDefinitions.FirstOrDefault(definition => definition.IsActive
                && definition.AreaId == id
                && VisitKindClassifier.Of(definition) == visitKind
                && definition.Recurrence?.Weekday is not null)
            : null;

        if (sibling is not null)
        {
            return sibling.Recurrence!.Weekday!.Value;
        }

        var capacity = WeeklyPlacementBuilder.RemainingCapacity(household, existingDefinitions, today);
        var newVisit = new PlaceableVisit(areaId, AreaName: null, visitKind, estimatedMinutes, effort, TaskDefinitionIds: []);
        var result = new WeeklyPlacementPlanner().Plan(new WeeklyPlacementRequest(capacity, [newVisit]));

        return result.PlacedVisits.Single().Day;
    }

    /// <summary>
    /// For a new MONTHLY task: which week of the month it lands on. Joins a sibling already on
    /// the same room, visit kind and weekday if one exists (so the two land on the exact same
    /// occurrence, not just the same weekday); otherwise defaults to
    /// <see cref="WeekOfMonth.First"/> - a single new task has no reason to prefer a later week.
    /// </summary>
    public static WeekOfMonth ChooseMonthlyWeek(
        IReadOnlyList<TaskDefinition> existingDefinitions, Guid? areaId, VisitKind visitKind, DayOfWeek day)
    {
        var sibling = areaId is { } id
            ? existingDefinitions.FirstOrDefault(definition => definition.IsActive
                && definition.AreaId == id
                && VisitKindClassifier.Of(definition) == visitKind
                && definition.Recurrence?.Weekday == day
                && definition.Recurrence?.MonthlyWeek is not null)
            : null;

        return sibling?.Recurrence!.MonthlyWeek ?? WeekOfMonth.First;
    }
}

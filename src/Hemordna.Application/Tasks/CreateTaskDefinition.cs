using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Time;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Describes a new piece of household work.</summary>
public sealed class CreateTaskDefinition
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly TimeProvider _timeProvider;

    public CreateTaskDefinition(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates the definition, or returns <c>null</c> when the household does not exist.
    /// An area or responsible member that does not belong to this household is rejected -
    /// silently accepting it would leak one household's data into another's task list.
    /// </summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId,
        NewTaskDefinition request,
        CancellationToken cancellationToken,
        DateOnly? today = null)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        if (request.AreaId is { } areaId && household.Areas.All(area => area.Id != areaId))
        {
            throw new ArgumentException(
                "The area does not belong to this household.", nameof(request));
        }

        if (request.DefaultResponsibleMemberId is { } memberId
            && household.Members.All(member => member.Id != memberId))
        {
            throw new ArgumentException(
                "The responsible member does not belong to this household.", nameof(request));
        }

        var now = _timeProvider.GetUtcNow();
        var recurrence = request.Recurrence;

        // "Planera veckan"s egen placeringsalgoritm avgör var - inte klienten. Ersätter den
        // gamla naiva "nästa veckodag i tur"-spridningen (roomSpreadIndex i Rum.razor/
        // RoomSheet.razor) - se docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen". Bara de nya
        // besöken placeras; inget befintligt flyttas.
        if (request.AutoPlaceWeekday && recurrence is { IsWeeklyRhythm: true })
        {
            // The client's own "today" when it sends one - see HouseholdEndpoints'
            // CompleteOccurrenceAsync remarks for why. Matters doubly here: it is both the
            // placement algorithm's anchor AND the new recurrence's StartDate, so a task created
            // just after the household's local midnight must not be anchored to the wrong day -
            // and wrong weekday - for the rest of its life. Falls back to the server's own date
            // when the client does not send one.
            var resolvedToday = today ?? HouseholdClock.Today(_timeProvider);
            var existing = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);
            var visitKind = VisitKindClassifier.Of(recurrence, request.Effort);

            var chosenDay = NewTaskWeekdayPlacement.Choose(
                household, existing, resolvedToday, request.AreaId, visitKind, request.Effort, request.EstimatedMinutes);

            recurrence = recurrence.Frequency == RecurrenceFrequency.Monthly
                ? RecurrenceRule.MonthlyOnWeekday(
                    resolvedToday,
                    NewTaskWeekdayPlacement.ChooseMonthlyWeek(existing, request.AreaId, visitKind, chosenDay),
                    chosenDay,
                    recurrence.Interval)
                : RecurrenceRule.Weekly(resolvedToday, chosenDay, recurrence.Interval);
        }

        var definition = TaskDefinition.Create(householdId, request.Name, request.EstimatedMinutes, now);

        definition.ChangeDescription(request.Description);
        definition.ChangePriority(request.Priority);
        definition.ChangeEffort(request.Effort);
        definition.AssignToArea(request.AreaId);
        definition.SetDefaultResponsibleMember(request.DefaultResponsibleMemberId);
        definition.SetCanBeDeferred(request.CanBeDeferred);
        definition.SetRotatingResponsibility(request.HasRotatingResponsibility);
        definition.SetRequiresMultiplePeople(request.RequiresMultiplePeople);
        definition.SetRequiresAdult(request.RequiresAdult);
        // Recurrence before PreferredWeekday: SetPreferredWeekday requires a Weekly/Monthly
        // recurrence to already be in place - see TaskDefinition's own remarks.
        definition.SetRecurrence(recurrence);
        definition.SetStaleAfterDays(request.StaleAfterDays);
        definition.SetPreferredWeekday(request.PreferredWeekday);
        definition.EnsurePreferredWeekdayMatchesRecurrence();

        await _definitions.AddAsync(definition, cancellationToken);

        return definition;
    }
}

/// <summary>The fields a new task definition is created from.</summary>
/// <param name="AutoPlaceWeekday">
/// When true and <see cref="Recurrence"/>.<see cref="RecurrenceRule.IsWeeklyRhythm"/> is true, the
/// server chooses the actual weekday (and, for Monthly, the week of the month) itself via the
/// same placement algorithm "Planera veckan" uses - <see cref="Recurrence"/>'s own
/// <c>StartDate</c>/<c>Weekday</c>/<c>MonthlyWeek</c> are ignored in that case; only its
/// <c>Frequency</c>/<c>Interval</c> matter. A sparse rule (Interval &gt; 1) is left completely
/// untouched instead - its own <c>StartDate</c> carries WHICH month or phase is meant, and this
/// machinery cannot express that without destroying it. See docs/ARCHITECTURE.md "Beslut:
/// Placeringsalgoritmen" and "Beslut: Glesa regler lämnas i fred".
/// </param>
public sealed record NewTaskDefinition(
    string Name,
    int EstimatedMinutes,
    string? Description = null,
    Guid? AreaId = null,
    TaskPriority Priority = TaskPriority.Normal,
    Guid? DefaultResponsibleMemberId = null,
    DayOfWeek? PreferredWeekday = null,
    bool CanBeDeferred = true,
    bool HasRotatingResponsibility = false,
    bool RequiresMultiplePeople = false,
    bool RequiresAdult = false,
    RecurrenceRule? Recurrence = null,
    int? StaleAfterDays = null,
    TaskEffort Effort = TaskEffort.Medium,
    bool AutoPlaceWeekday = false);

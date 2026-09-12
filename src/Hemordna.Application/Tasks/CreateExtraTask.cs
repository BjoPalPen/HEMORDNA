using Hemordna.Application.Households;
using Hemordna.Application.Realtime;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// "Extra uppgift" on Min dag: a one-off task someone wants to do today, on top of whatever is
/// already planned.
/// </summary>
/// <remarks>
/// Creating the definition and scheduling its first occurrence happen together, on the caller
/// themselves, in one operation - unlike <see cref="CreateTaskDefinition"/> (which only
/// describes a standing piece of household work, for whoever <c>POST /tasks</c> is called by)
/// composed with a separate <see cref="ScheduleTaskOccurrence"/> call. Keeping the two together
/// server-side, rather than relying on the client to always schedule what it just created for
/// itself, turns "my own, today" from a client convention into a guarantee - see
/// docs/ARCHITECTURE.md "Beslut: Vem får ändra vad" for why this needed its own endpoint at all:
/// <c>POST /tasks</c> itself is household configuration and requires
/// <see cref="Hemordna.Domain.Households.HouseholdMember.CanManageHousehold"/>, but adding an
/// extra task to one's own day is daily work that every member can do.
/// </remarks>
public sealed class CreateExtraTask
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IHouseholdNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public CreateExtraTask(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        IHouseholdNotifier notifier,
        TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates the definition and schedules it on <paramref name="memberId"/> for
    /// <paramref name="today"/>, or returns <c>null</c> when the household does not exist. An
    /// area that does not belong to this household is rejected, the same check
    /// <see cref="CreateTaskDefinition"/> makes.
    /// </summary>
    public async Task<TaskOccurrence?> HandleAsync(
        Guid householdId,
        Guid memberId,
        NewExtraTask request,
        DateOnly today,
        CancellationToken cancellationToken)
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

        var definition = TaskDefinition.Create(householdId, request.Name, request.EstimatedMinutes, _timeProvider.GetUtcNow());
        definition.ChangeDescription(request.Description);
        definition.AssignToArea(request.AreaId);

        // Always the caller's own - an extra task is, by definition, something added to one's
        // own day, never assigned to someone else on their behalf.
        definition.SetDefaultResponsibleMember(memberId);

        await _definitions.AddAsync(definition, cancellationToken);

        var occurrence = definition.ScheduleFor(today, _timeProvider.GetUtcNow());
        await _occurrences.AddAsync(occurrence, cancellationToken);

        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return occurrence;
    }
}

/// <summary>The fields a new extra task is created from - a small subset of <see cref="NewTaskDefinition"/>:
/// no recurrence, no rotation, no priority choice - an extra task is a one-off, for its creator alone.</summary>
public sealed record NewExtraTask(
    string Name,
    int EstimatedMinutes,
    string? Description = null,
    Guid? AreaId = null);

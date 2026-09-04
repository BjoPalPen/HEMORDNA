using Hemordna.Application.Households;
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

        if (request.DefaultResponsibleMemberId is { } memberId
            && household.Members.All(member => member.Id != memberId))
        {
            throw new ArgumentException(
                "The responsible member does not belong to this household.", nameof(request));
        }

        var definition = TaskDefinition.Create(
            householdId,
            request.Name,
            request.EstimatedMinutes,
            _timeProvider.GetUtcNow());

        definition.ChangeDescription(request.Description);
        definition.ChangePriority(request.Priority);
        definition.AssignToArea(request.AreaId);
        definition.SetDefaultResponsibleMember(request.DefaultResponsibleMemberId);
        definition.SetPreferredWeekday(request.PreferredWeekday);
        definition.SetCanBeDeferred(request.CanBeDeferred);
        definition.SetRotatingResponsibility(request.HasRotatingResponsibility);
        definition.SetRequiresMultiplePeople(request.RequiresMultiplePeople);

        await _definitions.AddAsync(definition, cancellationToken);

        return definition;
    }
}

/// <summary>The fields a new task definition is created from.</summary>
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
    bool RequiresMultiplePeople = false);

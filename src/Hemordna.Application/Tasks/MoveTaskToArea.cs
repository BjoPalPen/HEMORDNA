using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Moves an existing task to a different room, or clears the room ("Övrigt") - previously only
/// choosable at creation.
/// </summary>
public sealed class MoveTaskToArea
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;

    public MoveTaskToArea(IHouseholdRepository households, ITaskDefinitionRepository definitions)
    {
        _households = households;
        _definitions = definitions;
    }

    /// <summary>Updates the definition, or returns <c>null</c> when the household has no such task.</summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskId, Guid? areaId, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        if (areaId is { } id)
        {
            var household = await _households.FindByIdAsync(householdId, cancellationToken);

            if (household is null || household.Areas.All(area => area.Id != id))
            {
                throw new ArgumentException("The area does not belong to this household.", nameof(areaId));
            }
        }

        definition.AssignToArea(areaId);

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}

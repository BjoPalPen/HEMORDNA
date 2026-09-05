using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Removing a task is a deactivation, not a delete - see TaskDefinition for why.</summary>
public sealed class DeactivateTaskDefinition
{
    private readonly ITaskDefinitionRepository _definitions;

    public DeactivateTaskDefinition(ITaskDefinitionRepository definitions) => _definitions = definitions;

    /// <summary>Deactivates the task, or returns <c>null</c> when the household has no such task.</summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskDefinitionId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.Deactivate();
        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}

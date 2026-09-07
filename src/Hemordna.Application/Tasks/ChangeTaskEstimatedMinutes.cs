using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Changes an existing task's time estimate - see <see cref="TaskDefinition.ChangeEstimatedMinutes"/>.
/// Previously only choosable at creation.
/// </summary>
public sealed class ChangeTaskEstimatedMinutes
{
    private readonly ITaskDefinitionRepository _definitions;

    public ChangeTaskEstimatedMinutes(ITaskDefinitionRepository definitions) => _definitions = definitions;

    /// <summary>Updates the definition, or returns <c>null</c> when the household has no such task.</summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskId, int estimatedMinutes, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.ChangeEstimatedMinutes(estimatedMinutes);

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}

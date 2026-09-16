using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Changes how much an existing task takes out of whoever does it - see
/// <see cref="TaskDefinition.Effort"/>. Household configuration, same as time and frequency, so
/// this sits behind the same authorization as those - see docs/ARCHITECTURE.md "Beslut: Vem får
/// ändra vad".
/// </summary>
public sealed class ChangeTaskEffort
{
    private readonly ITaskDefinitionRepository _definitions;

    public ChangeTaskEffort(ITaskDefinitionRepository definitions) => _definitions = definitions;

    /// <summary>Updates the definition, or returns <c>null</c> when the household has no such task.</summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskId, TaskEffort effort, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.ChangeEffort(effort);

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}

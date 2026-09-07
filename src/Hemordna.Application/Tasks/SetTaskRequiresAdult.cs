using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Toggles whether an existing task's rotation should skip members whose role is a child - see
/// <see cref="TaskDefinition.RequiresAdult"/>. Previously only choosable at creation.
/// </summary>
public sealed class SetTaskRequiresAdult
{
    private readonly ITaskDefinitionRepository _definitions;

    public SetTaskRequiresAdult(ITaskDefinitionRepository definitions) => _definitions = definitions;

    /// <summary>Updates the definition, or returns <c>null</c> when the household has no such task.</summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskId, bool requiresAdult, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.SetRequiresAdult(requiresAdult);

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}

using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Removing a task is a deactivation, not a delete - see TaskDefinition for why.</summary>
/// <remarks>
/// Deactivating the definition is not enough on its own: occurrences already generated from it
/// keep sitting on someone's day, naming a task the household has said it no longer does. They
/// are skipped here, the same thing <see cref="Hemordna.Application.Households.PauseArea"/>
/// does for a paused room - except without an end date, because a removal has no "until".
/// Completed occurrences are never touched; the history of what WAS done stays true.
/// </remarks>
public sealed class DeactivateTaskDefinition
{
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;

    public DeactivateTaskDefinition(
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences)
    {
        _definitions = definitions;
        _occurrences = occurrences;
    }

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

        var outstanding = await _occurrences.ListOutstandingByHouseholdAsync(householdId, cancellationToken);

        foreach (var occurrence in outstanding.Where(o => o.TaskDefinitionId == taskDefinitionId))
        {
            occurrence.Skip();
            await _occurrences.UpdateAsync(occurrence, cancellationToken);
        }

        return definition;
    }
}

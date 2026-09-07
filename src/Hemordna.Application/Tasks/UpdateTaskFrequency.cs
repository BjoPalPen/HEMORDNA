using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Changes how often an existing task repeats. How often the same chore should happen varies
/// a lot between households (weekly here, monthly there), and today it can only be set once,
/// at creation - the household would otherwise have to deactivate the task and recreate it
/// just to change the cadence.
/// </summary>
public sealed class UpdateTaskFrequency
{
    private readonly ITaskDefinitionRepository _definitions;

    public UpdateTaskFrequency(ITaskDefinitionRepository definitions) => _definitions = definitions;

    /// <summary>
    /// Updates the definition, or returns <c>null</c> when the household has no such task.
    /// <paramref name="recurrence"/> and <paramref name="staleAfterDays"/> are mutually
    /// exclusive on the domain (a task is either on a calendar cadence or "as needed", never
    /// both) - passing a recurrence always clears any "as needed" interval, and vice versa.
    /// </summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId,
        Guid taskId,
        RecurrenceRule? recurrence,
        int? staleAfterDays,
        CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.SetRecurrence(recurrence);
        definition.SetStaleAfterDays(recurrence is null ? staleAfterDays : null);

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}

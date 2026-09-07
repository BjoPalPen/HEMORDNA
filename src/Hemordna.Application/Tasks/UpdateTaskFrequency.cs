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
    private readonly ITaskOccurrenceRepository _occurrences;

    public UpdateTaskFrequency(ITaskDefinitionRepository definitions, ITaskOccurrenceRepository occurrences)
    {
        _definitions = definitions;
        _occurrences = occurrences;
    }

    /// <summary>
    /// Updates the definition, or returns <c>null</c> when the household has no such task.
    /// <paramref name="recurrence"/> and <paramref name="staleAfterDays"/> are mutually
    /// exclusive on the domain (a task is either on a calendar cadence or "as needed", never
    /// both) - passing a recurrence always clears any "as needed" interval, and vice versa.
    /// </summary>
    /// <remarks>
    /// A schedule change retires whatever is currently outstanding for this task
    /// (<see cref="TaskOccurrence.Skip"/>). The occurrence's own <c>OriginalScheduledDate</c>
    /// was set under the OLD rule; left as-is it would sit forever, endlessly deferred, while
    /// <see cref="EnsureOccurrencesGenerated"/> also creates a fresh one for the NEW rule's
    /// slot the moment that date arrives - a permanent, nagging duplicate rather than one
    /// occurrence that simply moved. This is skipped only when the schedule actually changed:
    /// re-saving the same choice must not silently drop a task someone is already about to do
    /// today.
    /// </remarks>
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

        var newStaleAfterDays = recurrence is null ? staleAfterDays : null;
        var scheduleChanged = !Equals(definition.Recurrence, recurrence) || definition.StaleAfterDays != newStaleAfterDays;

        definition.SetRecurrence(recurrence);
        definition.SetStaleAfterDays(newStaleAfterDays);

        await _definitions.UpdateAsync(definition, cancellationToken);

        if (scheduleChanged)
        {
            var stale = await _occurrences.ListOutstandingOnOrBeforeAsync(
                householdId, taskId, DateOnly.MaxValue, cancellationToken);

            foreach (var occurrence in stale)
            {
                occurrence.Skip();
                await _occurrences.UpdateAsync(occurrence, cancellationToken);
            }
        }

        return definition;
    }
}

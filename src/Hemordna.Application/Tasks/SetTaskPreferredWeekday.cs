using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Locks an existing task to a weekday, or clears the lock - "Alltid på en viss veckodag"
/// (Björns krav: a chosen weekday is a requirement, not a wish, e.g. bin day on collection day).
/// </summary>
/// <remarks>
/// Setting a weekday changes the task's own <see cref="TaskDefinition.Recurrence"/> to that day
/// immediately, using the exact same "no duplicate, no skipped period" re-anchoring
/// <see cref="ApplyWeeklyPlan"/> already relies on (<see cref="RecurrenceReanchoring"/>) -
/// already-generated, outstanding occurrences are never touched (Björns krav: nya regler gäller
/// framåt). Clearing the lock only removes the requirement going forward; it does not, by
/// itself, move the task off the day it already happens to be on.
/// </remarks>
public sealed class SetTaskPreferredWeekday
{
    private readonly ITaskDefinitionRepository _definitions;

    public SetTaskPreferredWeekday(ITaskDefinitionRepository definitions) => _definitions = definitions;

    /// <summary>
    /// Updates the definition, or returns <c>null</c> when the household has no such task.
    /// Throws <see cref="Hemordna.Domain.Common.DomainException"/> (mapped to 409 by
    /// <c>DomainExceptionHandler</c>) when <paramref name="weekday"/> is non-null and the task's
    /// current recurrence is not Weekly or Monthly - see
    /// <see cref="TaskDefinition.SetPreferredWeekday"/>.
    /// </summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskId, DayOfWeek? weekday, DateOnly today, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        if (weekday is { } day)
        {
            RecurrenceReanchoring.LockToWeekday(definition, today, day);
        }
        else
        {
            definition.SetPreferredWeekday(null);
        }

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}
